using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
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
        public static async Task<ExecutePowerShellJob.Result> ExecuteScriptAsync(ILogSink logger, IOperationExecutionContext context, string fullScriptName, IReadOnlyDictionary<string, RuntimeValue> arguments, IDictionary<string, RuntimeValue> outArguments, bool collectOutput, EventHandler<PSProgressEventArgs> progressUpdateHandler)
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
                OutVariables = outArguments.Keys.ToArray()
            };

            job.MessageLogged += (s, e) => logger.Log(e.Level, e.Message);
            if (progressUpdateHandler != null)
                job.ProgressUpdate += progressUpdateHandler;

            var result = (ExecutePowerShellJob.Result)await jobRunner.ExecuteJobAsync(job, context.CancellationToken);
            if (result.ExitCode != null)
                logger.LogDebug("Script exit code: " + result.ExitCode);

            foreach (var var in result.OutVariables)
                outArguments[var.Key] = ToRuntimeValue(var.Value);

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
                raftName = RaftRepository.DefaultName;
                scriptName = scriptNameParts[0];
            }

            using (var raft = RaftRepository.OpenRaft(raftName))
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
    }
}
