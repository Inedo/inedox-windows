using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            var variables = new Dictionary<string, object>();
            var parameters = new Dictionary<string, object>();

            if (PowerShellScriptInfo.TryParse(new StringReader(scriptText), out var scriptInfo))
            {
                foreach (var var in arguments)
                {
                    var value = PowerShellScriptRunner.ConvertToPSValue(var.Value);
                    var param = scriptInfo.Parameters.FirstOrDefault(p => string.Equals(p.Name, var.Key, StringComparison.OrdinalIgnoreCase));
                    if (param != null && param.IsBooleanOrSwitch)
                        value = Convert.ToBoolean(value);
                    if (param != null)
                        parameters[param.Name] = value;
                    else
                        variables[var.Key] = value;
                }
            }
            else
            {
                variables = PowerShellScriptRunner.ConvertToPSArgs(arguments);
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

        private static RuntimeValue ToRuntimeValue(object value)
        {
            if (value is System.Collections.IDictionary dict)
                return new RuntimeValue(dict.Keys.Cast<object>().ToDictionary(k => k?.ToString(), k => ToRuntimeValue(dict[k])));
            if (value is string)
                return new RuntimeValue(value?.ToString());
            if (value is System.Collections.IEnumerable list)
                return new RuntimeValue(list.Cast<object>().Select(ToRuntimeValue));

            return new RuntimeValue(value?.ToString());
        }

#if Hedgehog
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
#elif BuildMaster
        private static Task<string> GetScriptTextAsync(ILogSink logger, string fullScriptName, IOperationExecutionContext context)
        {
            string scriptName;
            int? applicationId;
            var scriptNameParts = fullScriptName.Split(new[] { "::" }, 2, StringSplitOptions.None);
            if (scriptNameParts.Length == 2)
            {
                if (string.Equals(scriptNameParts[0], "GLOBAL", StringComparison.OrdinalIgnoreCase))
                {
                    applicationId = null;
                }
                else
                {
                    applicationId = Inedo.BuildMaster.Data.DB.Applications_GetApplications(null, true).FirstOrDefault(a => string.Equals(a.Application_Name, scriptNameParts[0], StringComparison.OrdinalIgnoreCase))?.Application_Id;
                    if (applicationId == null)
                    {
                        logger.LogError($"Invalid application name {scriptNameParts[0]}.");
                        return Task.FromResult<string>(null);
                    }
                }

                scriptName = scriptNameParts[1];
            }
            else
            {
                applicationId = (context as BuildMaster.Extensibility.IGenericBuildMasterContext)?.ApplicationId;
                scriptName = scriptNameParts[0];
            }

            if (!scriptName.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
                scriptName += ".ps1";

            var script = Inedo.BuildMaster.Data.DB.ScriptAssets_GetScriptByName(scriptName, applicationId);
            if (script == null)
            {
                logger.LogError($"Script {scriptName} not found.");
                return Task.FromResult<string>(null);
            }

            using (var stream = new MemoryStream(script.Script_Text, false))
            using (var reader = new StreamReader(stream, InedoLib.UTF8Encoding))
            {
                return Task.FromResult(reader.ReadToEnd());
            }
        }
#endif
    }
}
