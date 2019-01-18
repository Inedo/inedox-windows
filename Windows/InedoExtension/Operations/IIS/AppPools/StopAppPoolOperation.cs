using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Windows.Operations.IIS.AppPools
{
    [DisplayName("Stop App Pool")]
    [Description("Stops an IIS app pool.")]
    [ScriptAlias("Stop-AppPool")]
    [SeeAlso(typeof(StartAppPoolOperation))]
    [ScriptNamespace(Namespaces.IIS)]
    public sealed class StopAppPoolOperation : AppPoolOperationBase
    {
        internal override AppPoolOperationType OperationType => AppPoolOperationType.Stop;

        [ScriptAlias("WaitForStoppedStatus")]
        [DisplayName("Wait for stopped status")]
        [DefaultValue(true)]
        public override bool WaitForTargetStatus { get; set; } = true;

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
