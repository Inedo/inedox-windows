using System;
using System.Web.UI.WebControls;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.BuildMasterExtensions.Windows.Services
{
    internal sealed class StartServiceActionEditor : ActionEditorBase
    {
        private ValidatingTextBox txtService;
        private TextBox txtArgs;
        private CheckBox chkWaitForStart;
        private CheckBox chkIgnoreAlreadyStartedError;
        private CheckBox chkTreatStartErrorsAsWarnings;

        public override void BindToForm(ActionBase extension)
        {
            var ssa = (StartServiceAction)extension;
            this.txtService.Text = ssa.ServiceName;
            this.txtArgs.Text = (ssa.StartupArgs == null)
                ? string.Empty
                : string.Join(Environment.NewLine, ssa.StartupArgs);
            this.chkWaitForStart.Checked = ssa.WaitForStart;
            this.chkIgnoreAlreadyStartedError.Checked = ssa.IgnoreAlreadyStartedError;
            this.chkTreatStartErrorsAsWarnings.Checked = ssa.TreatUnableToStartAsWarning;
        }
        public override ActionBase CreateFromForm()
        {
            return new StartServiceAction
            {
                ServiceName = this.txtService.Text,
                StartupArgs = this.txtArgs.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries),
                WaitForStart = this.chkWaitForStart.Checked,
                IgnoreAlreadyStartedError = this.chkIgnoreAlreadyStartedError.Checked,
                TreatUnableToStartAsWarning = chkTreatStartErrorsAsWarnings.Checked
            };
        }

        protected override void CreateChildControls()
        {
            this.txtService = new ValidatingTextBox { Required = true };

            this.txtArgs = new TextBox
            {
                TextMode = TextBoxMode.MultiLine,
                Rows = 4
            };

            this.chkWaitForStart = new CheckBox
            {
                Text = "Wait until the service starts",
                Checked = true
            };

            this.chkIgnoreAlreadyStartedError = new CheckBox
            {
                Text = "Do not generate an error if service is already running",
                Checked = false
            };

            this.chkTreatStartErrorsAsWarnings = new CheckBox
            {
                Text = "Treat \"unable to start service\" condition as a warning instead of an error",
                Checked = false
            };

            this.Controls.Add(
                new SlimFormField("Service:", this.txtService),
                new SlimFormField("Startup arguments:", this.txtArgs)
                {
                    HelpText = "Enter arguments one per line."
                },
                new SlimFormField(
                    "Options:",
                    new Div(this.chkWaitForStart),
                    new Div(this.chkIgnoreAlreadyStartedError),
                    new Div(this.chkTreatStartErrorsAsWarnings)
                )
            );
        }
    }
}
