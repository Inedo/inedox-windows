using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.ExecutionEngine;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.RaftRepositories;

namespace Inedo.Extensions.Windows.PowerShell
{
    internal static class PSUtil
    {
        public static async Task<ExecutePowerShellJob.Result> ExecuteScriptAsync(ILogSink logger, IOperationExecutionContext context, string fullScriptName, IReadOnlyDictionary<string, RuntimeValue> arguments, IDictionary<string, RuntimeValue> outArguments, bool collectOutput, EventHandler<PSProgressEventArgs> progressUpdateHandler, string successExitCode = null)
        {
            var scriptText = await GetScriptTextAsync(logger, fullScriptName, context);

            var variables = new Dictionary<string, RuntimeValue>();
            var parameters = new Dictionary<string, RuntimeValue>();

            if (PowerShellScriptInfo.TryParse(new StringReader(scriptText), out var scriptInfo))
            {
                foreach (var var in arguments)
                {
                    var value = var.Value;
                    var param = scriptInfo.Parameters.FirstOrDefault(p => string.Equals(p.Name, var.Key, StringComparison.OrdinalIgnoreCase));
                    if (param != null && param.IsBooleanOrSwitch)
                        value = value.AsBoolean() ?? false;
                    if (param != null)
                        parameters[param.Name] = value;
                    else
                        variables[var.Key] = value;
                }
            }
            else
            {
                variables = arguments.ToDictionary(a => a.Key, a => a.Value);
            }

            var jobRunner = context.Agent.GetService<IRemoteJobExecuter>();

            var job = new ExecutePowerShellJob
            {
                ScriptText = scriptText,
                DebugLogging = false,
                VerboseLogging = true,
                CollectOutput = collectOutput,
                LogOutput = !collectOutput,
                Variables = variables,
                Parameters = parameters,
                OutVariables = outArguments.Keys.ToArray(),
                WorkingDirectory = context.WorkingDirectory
            };

            job.MessageLogged += (s, e) => logger.Log(e.Level, e.Message);
            if (progressUpdateHandler != null)
                job.ProgressUpdate += progressUpdateHandler;

            var result = (ExecutePowerShellJob.Result)await jobRunner.ExecuteJobAsync(job, context.CancellationToken);
            LogExit(logger, result.ExitCode, successExitCode);

            foreach (var var in result.OutVariables)
                outArguments[var.Key] = var.Value;

            return result;
        }

        public static RuntimeValue ToRuntimeValue(object value)
        {
            if (value is PSObject psObject)
            {
                if (psObject.BaseObject is IDictionary dictionary)
                    return new RuntimeValue(dictionary.Keys.Cast<object>().ToDictionary(k => k?.ToString(), k => ToRuntimeValue(dictionary[k])));

                if (psObject.BaseObject is IConvertible)
                    return new RuntimeValue(psObject.BaseObject.ToString());

                var d = new Dictionary<string, RuntimeValue>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in psObject.Properties)
                {
                    if (p.IsGettable && p.IsInstance)
                        d[p.Name] = ToRuntimeValue(p.Value);
                }

                return new RuntimeValue(d);
            }

            if (value is IDictionary dict)
                return new RuntimeValue(dict.Keys.Cast<object>().ToDictionary(k => k?.ToString(), k => ToRuntimeValue(dict[k])));

            if (value is IConvertible)
                return new RuntimeValue(value?.ToString());

            if (value is IEnumerable e)
            {
                var list = new List<RuntimeValue>();
                foreach (var item in e)
                    list.Add(ToRuntimeValue(item));

                return new RuntimeValue(list);
            }

            return new RuntimeValue(value?.ToString());
        }

        private static async Task<string> GetScriptTextAsync(ILogSink logger, string fullScriptName, IOperationExecutionContext context)
        {
            string scriptName;
            string raftName;
            var scriptNameParts = fullScriptName.Split(new[] { "::" }, 2, StringSplitOptions.None);
            if (scriptNameParts.Length == 2)
            {
                raftName = scriptNameParts[0];
                scriptName = scriptNameParts[1];
            }
            else
            {
                raftName = HACK_GetDefaultRaftName(context);
                scriptName = scriptNameParts[0];
            }

            using (var raft = await context.OpenRaftAsync(raftName, OpenRaftOptions.OptimizeLoadTime))
            {
                if (raft == null)
                {
                    logger.LogError($"Raft {raftName} not found.");
                    return null;
                }

                using (var scriptItem = await raft.OpenRaftItemAsync(RaftItemType.Script, scriptName + ".ps1", FileMode.Open, FileAccess.Read))
                {
                    if (scriptItem == null)
                    {
                        logger.LogError($"Script {scriptName}.ps1 not found in {raftName} raft.");
                        return null;
                    }

                    using (var reader = new StreamReader(scriptItem, InedoLib.UTF8Encoding))
                    {
                        var scriptText = reader.ReadToEnd();
                        logger.LogDebug($"Found script {scriptName}.ps1 in {raftName} raft.");
                        return scriptText;
                    }
                }
            }
        }
        private static string HACK_GetDefaultRaftName(IOperationExecutionContext context)
        {
            // this is really bad, but Otter doesn't expose any way to do this
            try
            {
                if (SDK.ProductName == "Otter")
                {
                    var executer = context.GetType().GetField("executer", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(context);
                    if (executer != null)
                        return (string)executer.GetType().GetField("defaultRaftName", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(executer);
                }
            }
            catch
            {
            }

            return RaftRepository.DefaultName;
        }

        public static void LogExit(ILogSink logger, int? exitCode, string successExitCode = null)
        {
            if (!exitCode.HasValue)
                return;

            var comparator = ExitCodeComparator.TryParse(successExitCode);
            if (comparator != null && !comparator.Evaluate(exitCode.Value))
            {
                logger.LogError("Script exit code: " + exitCode);
            }
            else
            {
                logger.LogDebug("Script exit code: " + exitCode);
            }
        }

        private sealed class ExitCodeComparator
        {
            private static readonly string[] ValidOperators = new[] { "=", "==", "!=", "<", ">", "<=", ">=" };

            private ExitCodeComparator(string op, int value)
            {
                this.Operator = op;
                this.Value = value;
            }

            public string Operator { get; }
            public int Value { get; }

            public static ExitCodeComparator TryParse(string s)
            {
                if (string.IsNullOrWhiteSpace(s))
                    return null;

                var match = Regex.Match(s, @"^\s*(?<1>[=<>!])*\s*(?<2>[0-9]+)\s*$", RegexOptions.ExplicitCapture);
                if (!match.Success)
                    return null;

                var op = match.Groups[1].Value;
                if (string.IsNullOrEmpty(op) || !ValidOperators.Contains(op))
                    op = "==";

                return new ExitCodeComparator(op, int.Parse(match.Groups[2].Value));
            }

            public bool Evaluate(int exitCode)
            {
                switch (this.Operator)
                {
                    case "=":
                    case "==":
                        return exitCode == this.Value;

                    case "!=":
                        return exitCode != this.Value;

                    case "<":
                        return exitCode < this.Value;

                    case ">":
                        return exitCode > this.Value;

                    case "<=":
                        return exitCode <= this.Value;

                    case ">=":
                        return exitCode >= this.Value;
                }

                return false;
            }
        }
    }
}
