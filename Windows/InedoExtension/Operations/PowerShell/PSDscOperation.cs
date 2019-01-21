using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.ExecutionEngine.Mapping;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Windows.Configurations.DSC;

namespace Inedo.Extensions.Windows.Operations.PowerShell
{
    [DisplayName("PSDsc")]
    [Description("Ensures the configuration of a specified PowerShell DSC Resource.")]
    [ScriptAlias("PSDsc")]
    [Tag(Tags.PowerShell)]
    [ScriptNamespace(Namespaces.PowerShell, PreferUnqualified = true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Note("This is a shorthand version of the Ensure-DscResource operation.")]
    [Note(@"The default argument for this operation is the DSC Resource Name and should follow the format: ""ModuleName::ResourceName"". "
        + @"If ""ModuleName::"" is omitted, the PSDesiredStateConfiguration module will be used.", Heading = "Default Argument: ResourceName")]
    [Note(@"Otter Specific: By default, Otter will use the Name property of the DSC Resource as the configuration key. If there is no Name "
            + @"property or you would like to override the default configuration key name, specify a property named """
            + ConfigurationKeyPropertyName + @""" with the value containing a string (or list of strings) "
            + @"indicating the name of the property (or properties) to be used as the unique configuration key.", Heading = "Configuration Key")]
    [Note("An argument may be explicitly converted to an integral type by prefixing the value with [type::<typeName>], where <typeName> is one of: int, uint, long, ulong, double, decimal. Normally this conversion is performed automatically and this is not necessary.")]
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
  PortsToListen: @(3322,4431,1123),
  Enabled: true
);")]
    [SeeAlso(typeof(EnsureDscResourceOperation))]
    public sealed class PSDscOperation : EnsureOperation<DscConfiguration>, ICustomArgumentMapper
    {
        /// <summary>
        /// Key name used to manually specify the Otter Configuration Key.
        /// </summary>
        public const string ConfigurationKeyPropertyName = "Otter_ConfigurationKey";

        private readonly Lazy<DscConfiguration> lazyTemplate;
        private static readonly LazyRegex IsArrayPropertyRegex = new LazyRegex(@"^\[[^\[\]]+\[\]\]$", RegexOptions.Compiled);

        public PSDscOperation() => this.lazyTemplate = new Lazy<DscConfiguration>(this.CreateTemplate);

        public RuntimeValue DefaultArgument { get; set; }
        public IReadOnlyDictionary<string, RuntimeValue> NamedArguments { get; set; }
        public IDictionary<string, RuntimeValue> OutArguments { get; set; }
        public new DscConfiguration Template => this.lazyTemplate.Value;

        private QualifiedName ResourceName => QualifiedName.Parse(this.DefaultArgument.AsString());

        public override PersistedConfiguration GetConfigurationTemplate() => this.lazyTemplate.Value;
        public override Task<PersistedConfiguration> CollectAsync(IOperationCollectionContext context) => Dsc.CollectAsync(context, this, this.Template);
        public override Task ConfigureAsync(IOperationExecutionContext context) => Dsc.ConfigureAsync(context, this, this.Template);

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config) => new ExtendedRichDescription(new RichDescription("PSDsc"));

        private DscConfiguration CreateTemplate()
        {
            string keyName = null;
            var desiredValues = new Dictionary<string, RuntimeValue>(StringComparer.OrdinalIgnoreCase);
            foreach (var arg in this.NamedArguments)
            {
                if (string.Equals(arg.Key, ConfigurationKeyPropertyName, StringComparison.OrdinalIgnoreCase))
                    keyName = arg.Value.AsString();
                else
                    desiredValues[arg.Key] = arg.Value;
            }

            return new DscConfiguration(desiredValues)
            {
                ModuleName = this.ResourceName.Namespace,
                ResourceName = this.ResourceName.Name,
                ConfigurationKeyName = keyName,
                InDesiredState = true
            };
        }
    }
}
