using System.Web.UI.WebControls;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.BuildMasterExtensions.Windows.Services
{
    internal sealed class InstallServiceActionEditor : ActionEditorBase
    {
        private FileBrowserTextBox txtExePath;
        private ValidatingTextBox txtArguments;
        private ValidatingTextBox txtServiceName;
        private ValidatingTextBox txtDisplayName;
        private ValidatingTextBox txtDescription;
        private ValidatingTextBox txtUserAccount;
        private PasswordTextBox txtPassword;
        private CheckBox chkRecreate;
        private CheckBox chkErrorIfAlreadyInstalled;

        public override void BindToForm(ActionBase extension)
        {
            var action = (InstallServiceAction)extension;
            this.txtExePath.Text = action.ExePath;
            this.txtArguments.Text = action.Arguments;
            this.txtServiceName.Text = action.ServiceName;
            this.txtDisplayName.Text = action.ServiceDisplayName;
            this.txtDescription.Text = action.ServiceDescription;
            this.txtUserAccount.Text = action.UserAccount;
            this.txtPassword.Text = action.Password;
            this.chkRecreate.Checked = action.Recreate;
            this.chkErrorIfAlreadyInstalled.Checked = action.ErrorIfAlreadyInstalled;
        }
        public override ActionBase CreateFromForm()
        {
            return new InstallServiceAction
            {
                ExePath = this.txtExePath.Text,
                Arguments = this.txtArguments.Text,
                ServiceName = this.txtServiceName.Text,
                ServiceDisplayName = this.txtDisplayName.Text,
                ServiceDescription = this.txtDescription.Text,
                UserAccount = Util.NullIf(this.txtUserAccount.Text, string.Empty),
                Password = Util.NullIf(this.txtPassword.Text, string.Empty),
                Recreate = this.chkRecreate.Checked,
                ErrorIfAlreadyInstalled = this.chkErrorIfAlreadyInstalled.Checked
            };
        }

        protected override void CreateChildControls()
        {
            this.txtExePath = new FileBrowserTextBox
            {                
                Required = true,
                DefaultText = @"ex: C:\Program Files\Example\MyExampleService.exe"
            };

            this.txtArguments = new ValidatingTextBox { DefaultText = "none" };

            this.txtServiceName = new ValidatingTextBox { Required = true };

            this.txtDisplayName = new ValidatingTextBox { Required = true };

            this.txtDescription = new ValidatingTextBox { DefaultText = "none" };

            this.txtUserAccount = new ValidatingTextBox { DefaultText = "NT AUTHORITY\\LocalSystem" };

            this.txtPassword = new PasswordTextBox();

            this.chkRecreate = new CheckBox
            {
                Text = "Reinstall if a service with the same name is already installed"
            };

            var ctlRecreateContainer = new Div(this.chkRecreate) { ID = "ctlRecreateContainer" };

            this.chkErrorIfAlreadyInstalled = new CheckBox
            {
                ID = "chkErrorIfAlreadyInstalled",
                Text = "Log error if service with same name is already installed"
            };

            this.Controls.Add(
                new SlimFormField("Service executable:", this.txtExePath),
                new SlimFormField("Executable arguments:", this.txtArguments),
                new SlimFormField("Service name:", this.txtServiceName),
                new SlimFormField("Service display name:", this.txtDisplayName),
                new SlimFormField("Service description:", this.txtDescription),
                new SlimFormField("User account:", this.txtUserAccount)
                {
                    HelpText = "Supply a user account which the service will run as. <i>NT AUTHORITY\\LocalSystem</i> is used if an account is not supplied. " +
                        "To use Network Service, enter <i>NT AUTHORITY\\NetworkService</i>.<br/><br/>" +
                        "If a built in service account is used, leave the password field blank."
                },
                new SlimFormField("User account password:", this.txtPassword),
                new SlimFormField(
                    "Options:",
                    new Div(this.chkErrorIfAlreadyInstalled),
                    ctlRecreateContainer
                ),
                new RenderJQueryDocReadyDelegator(
                    w =>
                    {
                        w.Write(
                            "$('#{0}').change(function(){{if($(this).attr('checked')) $('#{1}').hide(); else $('#{1}').show();}});",
                            this.chkErrorIfAlreadyInstalled.ClientID,
                            ctlRecreateContainer.ClientID
                        );

                        w.Write("$('#{0}').change();", this.chkErrorIfAlreadyInstalled.ClientID);
                    }
                )
            );
        }
    }
}