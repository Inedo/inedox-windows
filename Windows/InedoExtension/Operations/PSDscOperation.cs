using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.ExecutionEngine.Mapping;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Windows.PowerShell;
using Inedo.Serialization;

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
            + ConfigurationKeyPropertyName + @""" with the value containing a string (or list of strings) "
            + @"indicating the name of the property (or properties) to be used as the unique configuration key.", Heading = "Configuration Key")]
    [Note(@"All properties will be treated as strings, unless they can be parsed as a decimal, or appear to be a boolean (true, $true, false, $false), array literal (@(...)), or hash literal (@{...}).", Heading = "Strings and Values")]
    [Example(@"
# ensures the existence of a file on the server
PSDsc File (
  " + ConfigurationKeyPropertyName + @": DestinationPath,
  DestinationPath: C:\hdars\1000.txt,
  Contents: test file ensured
);

# runs a custom resource
PSDsc cHdarsResource::cHdars (
  " + ConfigurationKeyPropertyName + @": LocalServer,
  MaximumSessionLength: 1000,
  PortsToListen: ""`@(3322,4431,1123)"",
  Enabled: true
);")]
    public sealed class PSDscOperation : EnsureOperation<DictionaryConfiguration>, ICustomArgumentMapper
    {
        private const string ConfigurationKeyPropertyName = "Otter_ConfigurationKey";

        private bool inDesiredState;

        public RuntimeValue DefaultArgument { get; set; }
        public IReadOnlyDictionary<string, RuntimeValue> NamedArguments { get; set; }
        public IDictionary<string, RuntimeValue> OutArguments { get; set; }

        private QualifiedName ResourceName => QualifiedName.Parse(this.DefaultArgument.AsString());

        public override PersistedConfiguration GetConfigurationTemplate()
        {
            var desiredValues = new Dictionary<string, string>();
            foreach (var arg in this.NamedArguments)
                desiredValues[arg.Key] = arg.Value.AsString() ?? string.Empty;

            desiredValues.Remove(ConfigurationKeyPropertyName);

            return new DictionaryConfiguration(desiredValues);
        }

        public new DictionaryConfiguration Template => (DictionaryConfiguration)this.GetConfigurationTemplate();

        public override async Task<PersistedConfiguration> CollectAsync(IOperationCollectionContext context)
        {
            var fullScriptName = this.DefaultArgument.AsString();
            if (fullScriptName == null)
            {
                this.LogError("Bad or missing DSC Resource name.");
                return new DictionaryConfiguration();
            }

            var jobRunner = await context.Agent.GetServiceAsync<IRemoteJobExecuter>();

            var collectJob = new ExecutePowerShellJob
            {
                CollectOutput = true,
                OutVariables = new[] { ExecutePowerShellJob.CollectOutputAsDictionary },
                DebugLogging = true,
                Variables = new Dictionary<string, object>
                {
                    { "Name", this.ResourceName.Name },
                    { "$Property", GetHashTable(this.Template.Items) },
                    { "ModuleName", this.ResourceName.Namespace ?? "PSDesiredStateConfiguration" }
                },
                ScriptText = "Invoke-DscResource -Name $Name -Method Get -Property $Property -ModuleName $ModuleName"
            };
            this.LogDebug(collectJob.ScriptText);
            collectJob.MessageLogged += (s, e) => this.Log(e.Level, e.Message);

            var result = (ExecutePowerShellJob.Result)await jobRunner.ExecuteJobAsync(collectJob, context.CancellationToken);
            var collectValues = ((Dictionary<string, object>)result.OutVariables[ExecutePowerShellJob.CollectOutputAsDictionary]).ToDictionary(k => k.Key, k => k.Value?.ToString(), StringComparer.OrdinalIgnoreCase);

            var testJob = new ExecutePowerShellJob
            {
                CollectOutput = true,
                DebugLogging = true,
                Variables = new Dictionary<string, object>
                {
                    { "Name", this.ResourceName.Name },
                    { "$Property", GetHashTable(this.Template.Items) },
                    { "ModuleName", this.ResourceName.Namespace ?? "PSDesiredStateConfiguration" }
                },
                ScriptText = "Invoke-DscResource -Name $Name -Method Test -Property $Property -ModuleName $ModuleName"
            };
            this.LogDebug(testJob.ScriptText);
            testJob.MessageLogged += (s, e) => this.Log(e.Level, e.Message);

            var result2 = (ExecutePowerShellJob.Result)await jobRunner.ExecuteJobAsync(testJob, context.CancellationToken);
            var output = result2.Output;

            if (output.Count == 0)
            {
                this.LogError("Invoke-DscResource did not return any values.");
                return new DictionaryConfiguration();
            }

            bool? inDesiredState = output.Select(TryParseBool).LastOrDefault(o => o != null);
            if (inDesiredState == null)
            {
                this.LogError("Invoke-DscResource did not return a boolean value.");
                return new DictionaryConfiguration();
            }

            this.inDesiredState = (bool)inDesiredState;

            return new DictionaryConfiguration(collectValues);
        }

        private static bool? TryParseBool(string s)
        {
            bool b;
            if (bool.TryParse(s, out b))
                return b;
            else
                return null;
        }

        public override ComparisonResult Compare(PersistedConfiguration other)
        {
            if (this.inDesiredState)
                return new ComparisonResult(new Difference[0]);
            else
                return new ComparisonResult(new[] { new Difference("InDesiredState", true, false) });
        }

        public override async Task StoreConfigurationStatusAsync(PersistedConfiguration actual, ComparisonResult results, ConfigurationPersistenceContext context)
        {
            if (actual == null)
                throw new ArgumentNullException(nameof(actual));
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var config = (DictionaryConfiguration)actual;

            string keyName = this.ExtractConfigurationKeyName(config);
            string ensurePropertyValue = config.Items.FirstOrDefault(i => string.Equals(i.Key, "Ensure", StringComparison.OrdinalIgnoreCase))?.Value;

            string configXml = null;
            if (!string.Equals(ensurePropertyValue, bool.FalseString, StringComparison.OrdinalIgnoreCase))
                configXml = Persistence.SerializeToPersistedObjectXml(config);

            await context.SetConfigurationStatusAsync(
                typeName: "DSC-" + this.ResourceName,
                key: keyName,
                status: results.AreEqual ? ConfigurationStatus.Current : ConfigurationStatus.Drifted
            );

            await context.StoreConfigurationValueAsync(
                typeName: "DSC-" + this.ResourceName,
                key: keyName,
                value: configXml
            );
        }

        public string ExtractConfigurationKeyName(DictionaryConfiguration config)
        {
            string keyName = config.Items.FirstOrDefault(i => string.Equals(i.Key, "Name", StringComparison.OrdinalIgnoreCase))?.Value;

            if (string.IsNullOrEmpty(keyName))
            {
                var value = this.NamedArguments.GetValueOrDefault(ConfigurationKeyPropertyName);
                var rubbish = value.AsEnumerable() ?? new RuntimeValue[] { value };
                keyName = string.Join(":", rubbish.Select(r => r.AsString()));
            }

            if (string.IsNullOrEmpty(keyName))
            {
                throw new InvalidOperationException("The Name property of the DSC resource was not found and the operation is missing "
                    + $"a \"{ConfigurationKeyPropertyName}\" property whose value is the name of the DSC resource property (or properties) to "
                    + "uniquely identify this configuration.");
            }

            return keyName;
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
                Variables = new Dictionary<string, object>
                {
                    { "$Name", this.ResourceName.Name },
                    { "$Property", GetHashTable(this.Template.Items) },
                    { "$ModuleName", this.ResourceName.Namespace ?? "PSDesiredStateConfiguration" }
                },
                ScriptText = "Invoke-DscResource -Name $Name -Method Set -Property $Property -ModuleName $ModuleName"
            };
            this.LogDebug(job.ScriptText);

            job.MessageLogged += (s, e) => this.Log(e.Level, e.Message);

            await jobRunner.ExecuteJobAsync(job, context.CancellationToken);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(new RichDescription("PSDsc"));
        }

        private static object GetValueLiteral(string value)
        {
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(value, "$true", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.Equals(value, "$false", StringComparison.OrdinalIgnoreCase))
                return false;

            decimal maybeDecimal;
            if (decimal.TryParse(value, out maybeDecimal))
                return maybeDecimal;

            if (value.StartsWith("@"))
            {
                var ast = Parser.ParseInput(value, out var tokens, out var errors);
                if (errors.Length == 0)
                {
                    try
                    {
                        var scriptBlock = ast.GetScriptBlock();
                        scriptBlock.CheckRestrictedLanguage(new string[0], new string[0], false);
                        using (var runspace = RunspaceFactory.CreateRunspace(new InedoPSHost()))
                        {
                            runspace.Open();
                            using (var powershell = System.Management.Automation.PowerShell.Create())
                            {
                                powershell.Runspace = runspace;
                                powershell.AddScript("$x = " + value);
                                powershell.Invoke();
                                return powershell.Runspace.SessionStateProxy.GetVariable("x");
                            }
                        }
                    }
                    catch
                    {
                        // Process as a string
                    }
                }
            }

            return PowerShellScriptRunner.ConvertToPSValue(new RuntimeValue(value));
        }

        private static Dictionary<string, object> GetHashTable(IEnumerable<DictionaryConfigurationEntry> config)
        {
            return config.ToDictionary(c => c.Key, c => GetValueLiteral(c.Value), StringComparer.OrdinalIgnoreCase);
        }
    }
}
