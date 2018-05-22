using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.ExecutionEngine.Mapping;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Windows.Configurations.DSC;
using Inedo.Extensions.Windows.PowerShell;

namespace Inedo.Extensions.Windows.Operations.PowerShell
{
    [DisplayName("PSDsc")]
    [Description("Ensures the configuration of a specified DSC Resource.")]
    [ScriptAlias("PSDsc")]
    [Tag(Tags.PowerShell)]
    [ScriptNamespace(Namespaces.PowerShell, PreferUnqualified = true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Note(@"The default argument for this operation is the DSC Resource Name and should follow the format: ""ModuleName::ResourceName"". "
        + @"If ""ModuleName::"" is omitted, the PSDesiredStateConfiguration module will be used.", Heading = "Default Argument: ResourceName")]
    [Note(@"By default, Otter will use the Name property of the DSC Resource as the configuration key. If there is no Name "
            + @"property or you would like to override the default configuration key name, specify a property named """
            + DscConfiguration.ConfigurationKeyPropertyName + @""" with the value containing a string (or list of strings) "
            + @"indicating the name of the property (or properties) to be used as the unique configuration key.", Heading = "Configuration Key")]
    [Note(@"All properties will be treated as strings, unless they can be parsed as a decimal, or appear to be a boolean (true, $true, false, $false), array literal (@(...)), or hash literal (@{...}).", Heading = "Strings and Values")]
    [Example(@"
# ensures the existence of a file on the server
PSDsc File (
  " + DscConfiguration.ConfigurationKeyPropertyName + @": DestinationPath,
  DestinationPath: C:\hdars\1000.txt,
  Contents: test file ensured
);

# runs a custom resource
PSDsc cHdarsResource::cHdars (
  " + DscConfiguration.ConfigurationKeyPropertyName + @": LocalServer,
  MaximumSessionLength: 1000,
  PortsToListen: ""`@(3322,4431,1123)"",
  Enabled: true
);")]
    public sealed class PSDscOperation : EnsureOperation<DscConfiguration>, ICustomArgumentMapper
    {
        public RuntimeValue DefaultArgument { get; set; }
        public IReadOnlyDictionary<string, RuntimeValue> NamedArguments { get; set; }
        public IDictionary<string, RuntimeValue> OutArguments { get; set; }
        public new DscConfiguration Template => (DscConfiguration)this.GetConfigurationTemplate();

        private QualifiedName ResourceName => QualifiedName.Parse(this.DefaultArgument.AsString());

        public override PersistedConfiguration GetConfigurationTemplate()
        {
            string keyName = null;
            var desiredValues = new Dictionary<string, string>();
            foreach (var arg in this.NamedArguments)
            {
                if (string.Equals(arg.Key, DscConfiguration.ConfigurationKeyPropertyName, StringComparison.OrdinalIgnoreCase))
                    keyName = arg.Value.AsString();
                else
                    desiredValues[arg.Key] = arg.Value.AsString() ?? string.Empty;
            }

            return new DscConfiguration(desiredValues)
            {
                ResourceName = this.ResourceName.Name,
                ConfigurationKeyName = keyName,
                InDesiredState = true
            };
        }
        public override async Task<PersistedConfiguration> CollectAsync(IOperationCollectionContext context)
        {
            var template = this.Template;

            var fullScriptName = this.DefaultArgument.AsString();
            if (fullScriptName == null)
            {
                this.LogError("Bad or missing DSC Resource name.");
                return null;
            }

            var jobRunner = await context.Agent.GetServiceAsync<IRemoteJobExecuter>();

            var collectJob = new ExecutePowerShellJob
            {
                CollectOutput = true,
                OutVariables = new[] { ExecutePowerShellJob.CollectOutputAsDictionary },
                DebugLogging = true,
                ScriptText = "Invoke-DscResource -Name $Name -Method Get -Property $Property -ModuleName $ModuleName",
                Variables = new Dictionary<string, object>
                {
                    ["Name"] = this.ResourceName.Name,
                    ["Property"] = template.GetHashTable(),
                    ["ModuleName"] = this.ResourceName.Namespace ?? "PSDesiredStateConfiguration"
                }
            };

            this.LogDebug(collectJob.ScriptText);
            collectJob.MessageLogged += (s, e) => this.Log(e.Level, e.Message);

            var result = (ExecutePowerShellJob.Result)await jobRunner.ExecuteJobAsync(collectJob, context.CancellationToken);

            var collectValues = ((Dictionary<string, object>)result.OutVariables[ExecutePowerShellJob.CollectOutputAsDictionary])
                .Where(p => !string.IsNullOrEmpty(p.Value?.ToString()))
                .ToDictionary(k => k.Key, k => k.Value?.ToString(), StringComparer.OrdinalIgnoreCase);

            var testJob = new ExecutePowerShellJob
            {
                CollectOutput = true,
                DebugLogging = true,
                ScriptText = "Invoke-DscResource -Name $Name -Method Test -Property $Property -ModuleName $ModuleName",
                Variables = new Dictionary<string, object>
                {
                    ["Name"] = this.ResourceName.Name,
                    ["Property"] = template.GetHashTable(),
                    ["ModuleName"] = this.ResourceName.Namespace ?? "PSDesiredStateConfiguration"
                }
            };

            this.LogDebug(testJob.ScriptText);
            testJob.MessageLogged += (s, e) => this.Log(e.Level, e.Message);

            var result2 = (ExecutePowerShellJob.Result)await jobRunner.ExecuteJobAsync(testJob, context.CancellationToken);
            var output = result2.Output;

            if (output.Count == 0)
            {
                this.LogError("Invoke-DscResource did not return any values.");
                return null;
            }

            bool? inDesiredState = output
                .Select(s => bool.TryParse(s, out bool b) ? (bool?)b : null)
                .LastOrDefault(o => o != null);

            if (inDesiredState == null)
            {
                this.LogError("Invoke-DscResource did not return a boolean value.");
                return null;
            }

            return new DscConfiguration(collectValues)
            {
                ResourceName = this.Template.ResourceName,
                ConfigurationKeyName = this.Template.ConfigurationKeyName,
                InDesiredState = inDesiredState.Value
            };
        }
        public override async Task ConfigureAsync(IOperationExecutionContext context)
        {
            if (context.Simulation)
            {
                this.LogInformation("Invoking DscResource...");
                return;
            }

            var jobRunner = await context.Agent.GetServiceAsync<IRemoteJobExecuter>();

            var job = new ExecutePowerShellJob
            {
                DebugLogging = true,
                ScriptText = "Invoke-DscResource -Name $Name -Method Set -Property $Property -ModuleName $ModuleName",
                Variables = new Dictionary<string, object>
                {
                    ["Name"] = this.ResourceName.Name,
                    ["Property"] = this.Template.GetHashTable(),
                    ["ModuleName"] = this.ResourceName.Namespace ?? "PSDesiredStateConfiguration"
                }
            };

            this.LogDebug(job.ScriptText);
            job.MessageLogged += (s, e) => this.Log(e.Level, e.Message);

            await jobRunner.ExecuteJobAsync(job, context.CancellationToken);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config) => new ExtendedRichDescription(new RichDescription("PSDsc"));
    }
}
