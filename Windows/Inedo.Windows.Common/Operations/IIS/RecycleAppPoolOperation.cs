using System.ComponentModel;
using Inedo.Documentation;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
#endif

namespace Inedo.Extensions.Windows.Operations.IIS
{
    [DisplayName("Recycle App Pool")]
    [Description("Recycles an application pool.")]
    [ScriptAlias("Recycle-AppPool")]
    [ScriptNamespace(Namespaces.IIS)]
    public sealed class RecycleAppPoolOperation : AppPoolOperationBase
    {
        internal override AppPoolOperationType OperationType => AppPoolOperationType.Recycle;

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Recycle ",
                    new Hilite(config[nameof(ApplicationPoolName)]),
                    " App Pool"
                )
            );
        }
    }
}
