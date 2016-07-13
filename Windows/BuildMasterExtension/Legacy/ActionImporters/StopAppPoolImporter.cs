using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMasterExtensions.Windows.Iis;
using Inedo.Extensions.Windows.Operations.IIS;

namespace Inedo.BuildMasterExtensions.Windows.Legacy.ActionImporters
{
    class StopAppPoolImporter : IActionOperationConverter<ShutdownIisAppAction, StopAppPoolOperation>
    {
        public ConvertedOperation<StopAppPoolOperation> ConvertActionToOperation(ShutdownIisAppAction action, IActionConverterContext context)
        {
            return new StopAppPoolOperation
            {
                ApplicationPoolName = context.ConvertLegacyExpression(action.AppPool)
            };
        }
    }
}
