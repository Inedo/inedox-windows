using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Windows.Operations.IIS.AppPools
{
    [DisplayName("Recycle App Pool")]
    [Description("Recycles an application pool.")]
    [ScriptAlias("Recycle-AppPool")]
    [ScriptNamespace(Namespaces.IIS)]
    public sealed class RecycleAppPoolOperation : AppPoolOperationBase
    {
        internal override AppPoolOperationType OperationType => AppPoolOperationType.Recycle;

        [ScriptAlias("WaitForStartedStatus")]
        [DisplayName("Wait for started status")]
        [DefaultValue(true)]
        public override bool WaitForTargetStatus { get; set; } = true;

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
