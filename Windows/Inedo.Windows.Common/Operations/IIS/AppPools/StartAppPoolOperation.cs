using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

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
