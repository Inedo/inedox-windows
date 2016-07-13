using System.Web.UI.WebControls;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.BuildMasterExtensions.Windows.Services
{
    internal sealed class StopServiceActionEditor : ActionEditorBase
    {
        private ValidatingTextBox txtService;
        private CheckBox chkWaitForStop;
        private CheckBox chkIgnoreAlreadyStoppedError;

        public override void BindToForm(ActionBase extension)
        {
            var ssa = (StopServiceAction)extension;
            this.txtService.Text = ssa.ServiceName;
            this.chkWaitForStop.Checked = ssa.WaitForStop;
            this.chkIgnoreAlreadyStoppedError.Checked = ssa.IgnoreAlreadyStoppedError;
        }
        public override ActionBase CreateFromForm()
        {
            return new StopServiceAction
            {
                ServiceName = this.txtService.Text,
                WaitForStop = this.chkWaitForStop.Checked,
                IgnoreAlreadyStoppedError = this.chkIgnoreAlreadyStoppedError.Checked
            };
        }

        protected override void CreateChildControls()
        {
            this.txtService = new ValidatingTextBox { Required = true };

            this.chkWaitForStop = new CheckBox
            {
                Text = "Wait until the service stops",
                Checked = true
            };

            this.chkIgnoreAlreadyStoppedError = new CheckBox
            {
                Text = "Do not generate error if service is already stopped",
                Checked = true
            };

            this.Controls.Add(
                new SlimFormField("Service:", this.txtService),
                new SlimFormField(
                    "Options:",
                    new Div(this.chkWaitForStop),
                    new Div(this.chkIgnoreAlreadyStoppedError)
                )
            );
        }
    }
}
