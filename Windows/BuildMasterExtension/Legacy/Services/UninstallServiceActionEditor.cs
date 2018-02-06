using System.Web.UI.WebControls;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.BuildMasterExtensions.Windows.Services
{
    internal sealed class UninstallServiceActionEditor : ActionEditorBase
    {
        private ValidatingTextBox txtService;
        private CheckBox chkErrorIfNotInstalled;
        private CheckBox chkStopIfRunning;

        public override void BindToForm(ActionBase extension)
        {
            var action = (UninstallServiceAction)extension;
            this.txtService.Text = action.ServiceName;
            this.chkErrorIfNotInstalled.Checked = action.ErrorIfNotInstalled;
            this.chkStopIfRunning.Checked = action.StopIfRunning;
        }
        public override ActionBase CreateFromForm()
        {
            return new UninstallServiceAction
            {
                ServiceName = this.txtService.Text,
                ErrorIfNotInstalled = this.chkErrorIfNotInstalled.Checked,
                StopIfRunning = this.chkStopIfRunning.Checked
            };
        }

        protected override void CreateChildControls()
        {
            this.txtService = new ValidatingTextBox { Required = true };

            this.chkErrorIfNotInstalled = new CheckBox
            {
                Text = "Log error if service is not found"
            };

            this.chkStopIfRunning = new CheckBox
            {
                Text = "Stop service if it is running"
            };

            this.Controls.Add(
                new SlimFormField("Service:", this.txtService),
                new SlimFormField(
                    "Options:",
                    new Div(this.chkErrorIfNotInstalled),
                    new Div(this.chkStopIfRunning)
                )
            );
        }
    }
}
