using System.ComponentModel;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
#endif
using Inedo.Documentation;

namespace Inedo.Extensions.Windows.Operations.IIS.AppPools
{
    [DisplayName("Start App Pool")]
    [Description("Starts an application pool.")]
    [ScriptAlias("Start-AppPool")]
    [SeeAlso(typeof(StopAppPoolOperation))]
    [ScriptNamespace(Namespaces.IIS)]
    public sealed class StartAppPoolOperation : AppPoolOperationBase
    {
        internal override AppPoolOperationType OperationType => AppPoolOperationType.Start;

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Start ",
                    new Hilite(config[nameof(ApplicationPoolName)]),
                    " App Pool"
                )
            );
        }
    }
}
