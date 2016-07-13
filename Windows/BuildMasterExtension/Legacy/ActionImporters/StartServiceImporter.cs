using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.Extensions.Windows.Operations.Services;
using Inedo.BuildMasterExtensions.Windows.Services;

namespace Inedo.BuildMasterExtensions.Windows.ActionImporters
{
    internal sealed class StartServiceImporter : IActionOperationConverter<StartServiceAction, StartServiceOperation>
    {
        public ConvertedOperation<StartServiceOperation> ConvertActionToOperation(StartServiceAction action, IActionConverterContext context)
        {
            return new StartServiceOperation
            {
                ServiceName = context.ConvertLegacyExpression(action.ServiceName),
                WaitForRunningStatus = action.WaitForStart
            };
        }
    }
}
