using System.ComponentModel;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
#endif
using Inedo.Documentation;

namespace Inedo.Extensions.Windows.Operations.IIS.AppPools
{
    [DisplayName("Stop App Pool")]
    [Description("Stops an application pool.")]
    [ScriptAlias("Stop-AppPool")]
    [SeeAlso(typeof(StartAppPoolOperation))]
    [ScriptNamespace(Namespaces.IIS)]
    public sealed class StopAppPoolOperation : AppPoolOperationBase
    {
        internal override AppPoolOperationType OperationType => AppPoolOperationType.Stop;

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Stop ",
                    new Hilite(config[nameof(ApplicationPoolName)]),
                    " App Pool"
                )
            );
        }
    }
}
