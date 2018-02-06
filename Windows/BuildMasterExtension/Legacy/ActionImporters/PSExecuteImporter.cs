using Inedo.BuildMaster.Extensibility.Actions.Scripting;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMasterExtensions.Windows.Shell;
using Inedo.Extensions.Windows.Operations;

namespace Inedo.BuildMasterExtensions.Windows.ActionImporters
{
    internal sealed class PSExecuteImporter : IActionOperationConverter<ExecutePowerShellScriptAction, PSExecuteOperation>
    {
        public ConvertedOperation<PSExecuteOperation> ConvertActionToOperation(ExecutePowerShellScriptAction action, IActionConverterContext context)
        {
            if (action.ScriptMode != ScriptActionMode.Direct)
                return null;

            return new PSExecuteOperation
            {
                ScriptText = action.ScriptText,
                DebugLogging = true,
                VerboseLogging = true
            };
        }
    }
}
