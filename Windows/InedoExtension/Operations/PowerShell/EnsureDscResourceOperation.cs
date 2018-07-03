using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Windows.Configurations.DSC;

namespace Inedo.Extensions.Windows.Operations.PowerShell
{
    [Tag(Tags.PowerShell)]
    [DisplayName("Ensure DSC Resource")]
    [Description("Ensures the configuration of a specified PowerShell DSC Resource.")]
    [ScriptAlias("Ensure-DscResource")]
    [ScriptNamespace(Namespaces.PowerShell, PreferUnqualified = true)]
    [SeeAlso(typeof(PSDscOperation))]
    [Example(@"
# ensures the existence of a file on the server
Ensure-DscResource(
  Name: File,
  ConfigurationKey: DestinationPath,
  Properties: %(
    DestinationPath: C:\hdars\1000.txt,
    Contents: test file ensured)
);

# runs a custom resource
Ensure-DscResource(
  Name: cHdars,
  Module: cHdarsResource,
  ConfigurationKey: LocalServer,
  Properties: %(
    MaximumSessionLength: 1000,
    PortsToListen: @(3322,4431,1123),
    Enabled: true)
);")]
    public sealed class EnsureDscResourceOperation : EnsureOperation<DscConfiguration>
    {
        public override Task<PersistedConfiguration> CollectAsync(IOperationCollectionContext context) => Dsc.CollectAsync(context, this, this.GetTemplate());
        public override Task ConfigureAsync(IOperationExecutionContext context) => Dsc.ConfigureAsync(context, this, this.GetTemplate());

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config) => DscConfiguration.GetDescription(config);

        private DscConfiguration GetTemplate()
        {
            var t = this.Template;
            t.InDesiredState = true;
            return t;
        }
    }
}
