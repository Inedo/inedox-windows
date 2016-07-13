using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.IO;
using Inedo.Web.Controls;

namespace Inedo.BuildMasterExtensions.Windows.Shell
{
    internal sealed class ExecuteCScriptActionEditor : ActionEditorBase
    {
        private SourceControlFileFolderPicker ctlScriptPath;
        private ValidatingTextBox txtArguments;

        public override void BindToForm(ActionBase extension)
        {
            var execCScript = (ExecuteCScriptAction)extension;
            this.ctlScriptPath.Text = PathEx.Combine(execCScript.OverriddenSourceDirectory ?? string.Empty, execCScript.ScriptPath ?? string.Empty);
            this.txtArguments.Text = execCScript.Arguments;
        }

        public override ActionBase CreateFromForm()
        {
            return new ExecuteCScriptAction
            {
                OverriddenSourceDirectory = PathEx.GetDirectoryName(this.ctlScriptPath.Text),
                ScriptPath = PathEx.GetFileName(this.ctlScriptPath.Text),
                Arguments = this.txtArguments.Text
            };
        }

        protected override void CreateChildControls()
        {
            this.ctlScriptPath = new SourceControlFileFolderPicker { Required = true };

            this.txtArguments = new ValidatingTextBox();

            this.Controls.Add(
                new SlimFormField("Script file path:", this.ctlScriptPath),
                new SlimFormField("CScript arguments:", this.txtArguments)
            );
        }
    }
}
