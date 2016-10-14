using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Inedo.BuildMaster.Data;
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Extensibility.Actions.Scripting;
using Inedo.BuildMaster.Extensibility.Variables;
using Inedo.BuildMaster.Web;
using Inedo.BuildMasterExtensions.Windows.ActionImporters;
using Inedo.BuildMasterExtensions.Windows.Scripting.PowerShell;
using Inedo.Documentation;
using Inedo.Serialization;

namespace Inedo.BuildMasterExtensions.Windows.Shell
{
    [DisplayName("Execute PowerShell Script")]
    [Description("Runs a PowerShell script on the target server.")]
    [Tag("windows")]
    [CustomEditor(typeof(ExecutePowerShellScriptActionEditor))]
    [ConvertibleToOperation(typeof(PSExecuteImporter))]
    public sealed class ExecutePowerShellScriptAction : ExecuteScriptActionBase<PowerShellScriptType>, IMissingPersistentPropertyHandler
    {
        void IMissingPersistentPropertyHandler.OnDeserializedMissingProperties(IReadOnlyDictionary<string, string> missingProperties)
        {
            // Convert old format from before BM 4.1

            var variables = missingProperties.GetValueOrDefault("Variables") ?? string.Empty;
            var parameters = missingProperties.GetValueOrDefault("Parameters") ?? string.Empty;
            var script = missingProperties.GetValueOrDefault("Script");
            bool isScriptFile = bool.Parse(missingProperties.GetValueOrDefault("IsScriptFile", "False"));

            if (isScriptFile)
            {
                this.ScriptMode = ScriptActionMode.FileName;
                this.ScriptFileName = script;
            }
            else
            {
                this.ScriptMode = ScriptActionMode.Direct;
                this.ScriptText = script;
            }

            this.VariableValues = BuildDictionary(variables);
            this.ParameterValues = BuildDictionary(parameters);
        }

        protected override void Execute()
        {
            // Force defaults if specified in configurer
            var configurer = (WindowsExtensionConfigurer)this.GetExtensionConfigurer();
            if (configurer.OverridePowerShellDefaults)
            {
                var parameterData = DB.Scripts_GetScript(this.ScriptId)
                    .ScriptParameters
                    .Where(p => !string.IsNullOrEmpty(p.DefaultValue_Text));

                var application = DB.Applications_GetApplication(this.Context.ApplicationId)
                    .Applications_Extended
                    .FirstOrDefault();

                var evalContext = this.GetVariableEvaluationContext();

                foreach (var parameter in parameterData)
                {
                    string rubbish;
                    this.ParameterValues.TryGetValue(parameter.Parameter_Name, out rubbish);
                    if (string.IsNullOrEmpty(rubbish))
                    {
                        try
                        {
                            var tree = VariableExpressionTree.Parse(parameter.DefaultValue_Text, application.VariableSupport_Code);
                            this.ParameterValues[parameter.Parameter_Name] = tree.Evaluate(evalContext);
                        }
                        catch
                        {
                            this.ParameterValues[parameter.Parameter_Name] = parameter.DefaultValue_Text;
                        }
                    }
                }
            }

            base.Execute();
        }

        private static IDictionary<string, string> BuildDictionary(string values)
        {
            return Persistence.DeserializeToStringArray(values)
                .Select(s => s.Split(new[] { '=' }, 2))
                .Where(p => p.Length == 2)
                .Distinct(p => p[0], StringComparer.OrdinalIgnoreCase)
                .ToDictionary(p => p[0], p => p[1], StringComparer.OrdinalIgnoreCase);
        }

        private ILegacyVariableEvaluationContext GetVariableEvaluationContext()
        {
            // StandardEvaluationContext should probably be moved to SDK.
            // For now create instance using reflection.

            return (ILegacyVariableEvaluationContext)Activator.CreateInstance(
                Type.GetType("Inedo.BuildMaster.Variables.LegacyVariableEvaluationContext,BuildMaster", true),
                (IGenericBuildMasterContext)this.Context,
                this.Context.Variables
            );
        }
    }
}
