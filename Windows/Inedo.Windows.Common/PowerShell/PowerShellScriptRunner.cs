using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
#if BuildMaster
using Inedo.BuildMaster.Extensibility.Operations;
#elif Otter
using Inedo.Otter.Extensibility.Operations;
#endif
using Inedo.Diagnostics;
using Inedo.ExecutionEngine;

namespace Inedo.Extensions.Windows.PowerShell
{
    internal class PowerShellScriptRunner : ILogger, IDisposable
    {
        private InedoPSHost pshost = new InedoPSHost();
        private Lazy<Runspace> runspaceFactory;
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

        private Runspace InitializeRunspace()
        {
            var runspace = RunspaceFactory.CreateRunspace(this.pshost);
            runspace.Open();
            return runspace;
        }

        public static Dictionary<string, object> ExtractVariables(string script, IOperationExecutionContext context)
        {
            var vars = ExtractVariablesInternal(script);
            var results = new Dictionary<string, object>();
            foreach (var var in vars)
            {
                if (RuntimeVariableName.IsLegalVariableName(var))
                {
                    var varName = new RuntimeVariableName(var, RuntimeValueType.Scalar);
                    var varValue = context.TryGetVariableValue(varName) ?? TryGetFunctionValue(varName, context);
                    if (varValue != null)
                        results[var] = varValue.Value.AsString();
                }
            }

            return results;
        }

#if Otter
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
#elif BuildMaster
        private static RuntimeValue? TryGetFunctionValue(RuntimeVariableName functionName, IOperationExecutionContext context)
        {
            try
            {
                return context.TryEvaluateFunction(functionName, new RuntimeValue[0]);
            }
            catch
            {
                return null;
            }
        }
#endif

        public Task<int?> RunAsync(string script, CancellationToken cancellationToken)
        {
            return this.RunAsync(script, new Dictionary<string, object>(), new Dictionary<string, object>(), cancellationToken);
        }
        public Task<int?> RunAsync(string script, Dictionary<string, object> variables, Dictionary<string, object> outVariables, CancellationToken cancellationToken)
        {
            return this.RunAsync(script, new Dictionary<string, object>(), new Dictionary<string, object>(), new Dictionary<string, object>(), cancellationToken);
        }
        public async Task<int?> RunAsync(string script, Dictionary<string, object> variables, Dictionary<string, object> parameters, Dictionary<string, object> outVariables, CancellationToken cancellationToken)
        {
            var runspace = this.Runspace;

            var powerShell = System.Management.Automation.PowerShell.Create();
            powerShell.Runspace = runspace;

            foreach (var var in variables)
            {
                this.LogDebug($"Importing {var.Key}...");
                runspace.SessionStateProxy.SetVariable(var.Key, var.Value);
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
                powerShell.AddParameter(p.Key, p.Value);
            }

            int? exitCode = null;
            EventHandler<ShouldExitEventArgs> handleShouldExit = (s, e) => exitCode = e.ExitCode;
            this.pshost.ShouldExit += handleShouldExit;
            using (var registration = cancellationToken.Register(powerShell.Stop))
            {
                try
                {
                    await Task.Factory.FromAsync(powerShell.BeginInvoke((PSDataCollection<PSObject>)null, output), powerShell.EndInvoke);

                    foreach (var var in outVariables.Keys.ToList())
                        outVariables[var] = powerShell.Runspace.SessionStateProxy.GetVariable(var);
                }
                finally
                {
                    this.pshost.ShouldExit -= handleShouldExit;
                }
            }

            return exitCode;
        }

        public void Dispose()
        {
            if (!this.disposed && this.runspaceFactory.IsValueCreated)
            {
                this.runspaceFactory.Value.Close();
                this.runspaceFactory.Value.Dispose();
                this.disposed = true;
            }
        }

        public void Log(MessageLevel logLevel, string message) => this.MessageLogged?.Invoke(this, new LogMessageEventArgs(logLevel, message));

        private static IEnumerable<string> ExtractVariablesInternal(string script)
        {
            var variableRegex = new Regex(@"(?>\$(?<1>[a-zA-Z0-9_]+)|\${(?<2>[^}]+)})", RegexOptions.ExplicitCapture);

            Collection<PSParseError> errors;
            var tokens = PSParser.Tokenize(script, out errors);
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
                var matches = variableRegex.Matches(s);
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

        public static Dictionary<string, object> ConvertToPSArgs(IReadOnlyDictionary<string, RuntimeValue> args)
        {
            var result = new Dictionary<string, object>(args.Count);
            foreach (var pair in args)
                result[pair.Key] = ConvertToPSValue(pair.Value);

            return result;
        }
        public static object ConvertToPSValue(RuntimeValue value)
        {
            if (value.ValueType == RuntimeValueType.Scalar)
            {
                return value.AsString() ?? string.Empty;
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

        private void OnOutputReceived(PSObject obj) => this.OutputReceived?.Invoke(this, new PowerShellOutputEventArgs(obj));
        private void OnProgressUpdate(ProgressRecord p) => this.ProgressUpdate?.Invoke(this, new PSProgressEventArgs(p.PercentComplete, p.Activity ?? string.Empty));
    }
}
