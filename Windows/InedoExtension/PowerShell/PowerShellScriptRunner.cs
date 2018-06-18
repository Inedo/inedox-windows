using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.ExecutionEngine;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Windows.PowerShell
{
    internal class PowerShellScriptRunner : ILogger, IDisposable
    {
        private static readonly LazyRegex VariableRegex = new LazyRegex(@"(?>\$(?<1>[a-zA-Z0-9_]+)|\${(?<2>[^}]+)})", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        private readonly InedoPSHost pshost = new InedoPSHost();
        private readonly Lazy<Runspace> runspaceFactory;
        private bool disposed;

        public PowerShellScriptRunner()
        {
            this.pshost.MessageLogged += (s, e) => this.Log(e.Level, e.Message);
            this.runspaceFactory = new Lazy<Runspace>(this.InitializeRunspace);
        }

        public event EventHandler<PowerShellOutputEventArgs> OutputReceived;
        public event EventHandler<LogMessageEventArgs> MessageLogged;
        public event EventHandler<PSProgressEventArgs> ProgressUpdate;

        public Runspace Runspace => this.runspaceFactory.Value;
        public bool DebugLogging { get; set; }
        public bool VerboseLogging { get; set; }

        public static Dictionary<string, RuntimeValue> ExtractVariables(string script, IOperationExecutionContext context)
        {
            var vars = ExtractVariablesInternal(script);
            var results = new Dictionary<string, RuntimeValue>(StringComparer.OrdinalIgnoreCase);
            foreach (var var in vars)
            {
                if (RuntimeVariableName.IsLegalVariableName(var))
                {
                    var varName = new RuntimeVariableName(var, RuntimeValueType.Scalar);
                    var varValue = context.TryGetVariableValue(varName) ?? TryGetFunctionValue(varName, context);
                    if (varValue.HasValue)
                        results[var] = varValue.Value;
                }
            }

            return results;
        }
        public static object ConvertToPSValue(RuntimeValue value)
        {
            if (value.ValueType == RuntimeValueType.Scalar)
            {
                var s = value.AsString() ?? string.Empty;
                if (s.StartsWith(Functions.PsCredentialVariableFunction.Prefix))
                    return Functions.PsCredentialVariableFunction.Deserialize(s.Substring(Functions.PsCredentialVariableFunction.Prefix.Length));

                if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase))
                    return false;

                return s;
            }
            else if (value.ValueType == RuntimeValueType.Vector)
            {
                return value.AsEnumerable().Select(v => v.ToString() ?? string.Empty).ToArray();
            }
            else if (value.ValueType == RuntimeValueType.Map)
            {
                var hashTable = new Hashtable();
                foreach (var pair in value.AsDictionary())
                    hashTable[pair.Key] = ConvertToPSValue(pair.Value);

                return hashTable;
            }
            else
            {
                return null;
            }
        }

        public async Task<int?> RunAsync(string script, Dictionary<string, RuntimeValue> variables = null, Dictionary<string, RuntimeValue> parameters = null, Dictionary<string, RuntimeValue> outVariables = null, CancellationToken cancellationToken = default)
        {
            variables = variables ?? new Dictionary<string, RuntimeValue>();
            parameters = parameters ?? new Dictionary<string, RuntimeValue>();
            outVariables = outVariables ?? new Dictionary<string, RuntimeValue>();

            var runspace = this.Runspace;

            var powerShell = System.Management.Automation.PowerShell.Create();
            powerShell.Runspace = runspace;

            foreach (var var in variables)
            {
                this.LogDebug($"Importing {var.Key}...");
                runspace.SessionStateProxy.SetVariable(var.Key, ConvertToPSValue(var.Value));
            }

            if (this.DebugLogging)
                runspace.SessionStateProxy.SetVariable("DebugPreference", "Continue");

            if (this.VerboseLogging)
                runspace.SessionStateProxy.SetVariable("VerbosePreference", "Continue");

            var output = new PSDataCollection<PSObject>();
            output.DataAdded +=
                (s, e) =>
                {
                    var rubbish = output[e.Index];
                    this.OnOutputReceived(rubbish);
                };

            powerShell.Streams.Progress.DataAdded += (s, e) => this.OnProgressUpdate(powerShell.Streams.Progress[e.Index]);

            powerShell.Streams.AttachLogging(this);
            powerShell.AddScript(script);

            foreach (var p in parameters)
            {
                this.LogDebug($"Assigning parameter {p.Key}...");
                powerShell.AddParameter(p.Key, ConvertToPSValue(p.Value));
            }

            int? exitCode = null;
            this.pshost.ShouldExit += handleShouldExit;
            using (var registration = cancellationToken.Register(powerShell.Stop))
            {
                try
                {
                    await Task.Factory.FromAsync(powerShell.BeginInvoke((PSDataCollection<PSObject>)null, output), powerShell.EndInvoke);

                    foreach (var var in outVariables.Keys.ToList())
                        outVariables[var] = PSUtil.ToRuntimeValue(unwrapReference(powerShell.Runspace.SessionStateProxy.GetVariable(var)));
                }
                finally
                {
                    this.pshost.ShouldExit -= handleShouldExit;
                }
            }

            void handleShouldExit(object s, ShouldExitEventArgs e) => exitCode = e.ExitCode;

            object unwrapReference(object value) => value is PSReference reference ? reference.Value : value;

            return exitCode;
        }
        public void Log(MessageLevel logLevel, string message) => this.MessageLogged?.Invoke(this, new LogMessageEventArgs(logLevel, message));
        public void Dispose()
        {
            if (!this.disposed && this.runspaceFactory.IsValueCreated)
            {
                this.runspaceFactory.Value.Close();
                this.runspaceFactory.Value.Dispose();
                this.disposed = true;
            }
        }

        private Runspace InitializeRunspace()
        {
            var runspace = RunspaceFactory.CreateRunspace(this.pshost);
            runspace.Open();
            return runspace;
        }

        private static RuntimeValue? TryGetFunctionValue(RuntimeVariableName functionName, IOperationExecutionContext context)
        {
            try
            {
                return context.TryGetFunctionValue(functionName.ToString());
            }
            catch
            {
                return null;
            }
        }
        private static IEnumerable<string> ExtractVariablesInternal(string script)
        {
            var tokens = PSParser.Tokenize(script, out var errors);
            if (tokens == null)
                return Enumerable.Empty<string>();

            var vars = tokens
                .Where(v => v.Type == PSTokenType.Variable)
                .Select(v => v.Content)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var strings = tokens
                .Where(t => t.Type == PSTokenType.String)
                .Select(t => t.Content);

            foreach (var s in strings)
            {
                var matches = VariableRegex.Matches(s);
                if (matches.Count > 0)
                {
                    foreach (Match match in matches)
                    {
                        if (match.Groups[1].Success)
                            vars.Add(match.Groups[1].Value);
                        else if (match.Groups[2].Success)
                            vars.Add(match.Groups[2].Value);
                    }
                }
            }

            return vars;
        }

        private void OnOutputReceived(PSObject obj) => this.OutputReceived?.Invoke(this, new PowerShellOutputEventArgs(obj));
        private void OnProgressUpdate(ProgressRecord p) => this.ProgressUpdate?.Invoke(this, new PSProgressEventArgs(p.PercentComplete, p.Activity ?? string.Empty));
    }
}
