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
    [Note(@"Otter Specific: By default, Otter will use the Name property of the DSC Resource as the configuration key. If there is no Name "
            + @"property or you would like to override the default configuration key name, specify a property named """
            + DscConfiguration.ConfigurationKeyPropertyName + @""" with the value containing a string (or list of strings) "
            + @"indicating the name of the property (or properties) to be used as the unique configuration key.", Heading = "Configuration Key")]
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
  PortsToListen: @(3322,4431,1123),
  Enabled: true
);")]
    public sealed class PSDscOperation : EnsureOperation<DscConfiguration>, ICustomArgumentMapper
    {
        private readonly Lazy<DscConfiguration> lazyTemplate;

        public PSDscOperation() => this.lazyTemplate = new Lazy<DscConfiguration>(this.CreateTemplate);

        public RuntimeValue DefaultArgument { get; set; }
        public IReadOnlyDictionary<string, RuntimeValue> NamedArguments { get; set; }
        public IDictionary<string, RuntimeValue> OutArguments { get; set; }
        public new DscConfiguration Template => this.lazyTemplate.Value;

        private QualifiedName ResourceName => QualifiedName.Parse(this.DefaultArgument.AsString());

        public override PersistedConfiguration GetConfigurationTemplate() => this.lazyTemplate.Value;
        public override async Task<PersistedConfiguration> CollectAsync(IOperationCollectionContext context)
        {
            var fullScriptName = this.DefaultArgument.AsString();
            if (fullScriptName == null)
            {
                this.LogError("Bad or missing DSC Resource name.");
                return null;
            }

            var jobRunner = await context.Agent.GetServiceAsync<IRemoteJobExecuter>();

            var collectJob = this.CreateJob("Get", true);

            this.LogDebug(collectJob.ScriptText);
            collectJob.MessageLogged += (s, e) => this.Log(e.Level, e.Message);

            var result = (ExecutePowerShellJob.Result)await jobRunner.ExecuteJobAsync(collectJob, context.CancellationToken);

            var collectValues = result.OutVariables[ExecutePowerShellJob.CollectOutputAsDictionary].AsDictionary()
                .Where(p => p.Value.ValueType != RuntimeValueType.Scalar || !string.IsNullOrEmpty(p.Value.AsString()))
                .ToDictionary(k => k.Key, k => k.Value, StringComparer.OrdinalIgnoreCase);

            var testJob = this.CreateJob("Test");

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

            var job = this.CreateJob("Set");
            this.LogDebug(job.ScriptText);
            job.MessageLogged += (s, e) => this.Log(e.Level, e.Message);

            await jobRunner.ExecuteJobAsync(job, context.CancellationToken);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config) => new ExtendedRichDescription(new RichDescription("PSDsc"));

        private ExecutePowerShellJob CreateJob(string method, bool outputAsDictionary = false)
        {
            var job = new ExecutePowerShellJob
            {
                CollectOutput = true,
                DebugLogging = true,
                ScriptText = $"Invoke-DscResource -Name $Name -Method {method} -Property $Property -ModuleName $ModuleName",
                Variables = new Dictionary<string, RuntimeValue>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Name"] = this.ResourceName.Name,
                    ["Property"] = new RuntimeValue(this.Template.ToPowerShellDictionary()),
                    ["ModuleName"] = this.ResourceName.Namespace ?? "PSDesiredStateConfiguration"
                }
            };

            if (outputAsDictionary)
                job.OutVariables = new[] { ExecutePowerShellJob.CollectOutputAsDictionary };

            return job;
        }
        private DscConfiguration CreateTemplate()
        {
            string keyName = null;
            var desiredValues = new Dictionary<string, RuntimeValue>(StringComparer.OrdinalIgnoreCase);
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
    }
}
