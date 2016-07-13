using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.BuildMasterExtensions.Windows.Shell;

namespace Inedo.BuildMasterExtensions.Windows.Scripting.PowerShell
{
    internal sealed class ExecutePowerShellScriptActionEditor : ExecuteScriptActionEditor<PowerShellScriptType, ExecutePowerShellScriptAction>
    {
        protected override bool ShowVariables => true;
    }
}
