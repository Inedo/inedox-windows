using System;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.BuildMasterExtensions.Windows.Iis
{
    internal sealed class CreateIisAppPoolActionEditor : ActionEditorBase
    {
        private ValidatingTextBox txtName;
        private DropDownList ddlUser;
        private ValidatingTextBox txtUser;
        private PasswordTextBox txtPassword;
        private Div divUser;
        private RadioButtonList rblIntegrated;
        private DropDownList ddlManagedRuntimeVersion;

        public override void BindToForm(ActionBase extension)
        {
            var action = (CreateIisAppPoolAction)extension;

            this.txtName.Text = action.Name;
            if (new[] { "LocalSystem", "LocalService", "NetworkService", "ApplicationPoolIdentity" }.Contains(action.User, StringComparer.OrdinalIgnoreCase))
            {
                this.ddlUser.SelectedValue = action.User;
            }
            else
            {
                this.ddlUser.SelectedValue = "custom";
                this.txtUser.Text = action.User;
                this.txtPassword.Text = action.Password;
            }

            this.rblIntegrated.SelectedValue = action.IntegratedMode.ToString().ToLower();
            this.ddlManagedRuntimeVersion.SelectedValue = action.ManagedRuntimeVersion;
        }

        public override ActionBase CreateFromForm()
        {
            return new CreateIisAppPoolAction()
            {
                Name = this.txtName.Text,
                User = this.ddlUser.SelectedValue == "custom" ? this.txtUser.Text : this.ddlUser.SelectedValue,
                Password = this.ddlUser.SelectedValue == "custom" ? this.txtPassword.Text : "",
                IntegratedMode = bool.Parse(this.rblIntegrated.SelectedValue),
                ManagedRuntimeVersion = this.ddlManagedRuntimeVersion.SelectedValue
            };
        }

        protected override void OnPreRender(EventArgs e)
        {
            this.Controls.Add(GetClientSideScript(this.ddlUser.ClientID, this.divUser.ClientID));

            base.OnPreRender(e);
        }

        protected override void CreateChildControls()
        {
            this.txtName = new ValidatingTextBox { Required = true };
            this.ddlUser = new DropDownList
            {
                Items =
                {
                    new ListItem("Local System", "LocalSystem"),
                    new ListItem("Local Service", "LocalService"),
                    new ListItem("Network Service", "NetworkService"),
                    new ListItem("Application Pool Identity", "ApplicationPoolIdentity"),
                    new ListItem("Custom...", "custom")
                }
            };

            this.txtUser = new ValidatingTextBox();
            this.txtPassword = new PasswordTextBox();

            this.divUser = new Div
            { 
                Controls = 
                { 
                    new LiteralControl("<br />"),
                    new SlimFormField("User name:", this.txtUser), 
                    new SlimFormField("Password:", this.txtPassword)
                } 
            };

            this.rblIntegrated = new RadioButtonList
            {
                Items = 
                { 
                    new ListItem("Integrated Mode", "true"),
                    new ListItem("Classic Mode", "false") 
                }
            };

            this.ddlManagedRuntimeVersion = new DropDownList
            {
                Items =
                {
                    new ListItem("v4.0"),
                    new ListItem("v2.0")
                }
            };

            this.Controls.Add(
                new SlimFormField(
                    "Application pool name:",
                    this.txtName
                ),
                new SlimFormField(
                    "User identity:",
                    this.ddlUser,
                    this.divUser
                ),
                new SlimFormField(
                    "Managed pipeline mode:",
                    this.rblIntegrated
                ),
                new SlimFormField(
                    "Managed runtime version:",
                    this.ddlManagedRuntimeVersion
                )
            );
        }

        private RenderJQueryDocReadyDelegator GetClientSideScript(string ddlUserId, string divUserId)
        {
            return new RenderJQueryDocReadyDelegator(w =>
                w.Write(
                    "var onload = $('#" + ddlUserId + "').find('option').filter(':selected').val();" +
                    "if(onload == 'custom')" +
                    "{" +
                        "$('#" + divUserId + "').show();" +
                    "}" +

                    "$('#" + ddlUserId + "').change(function () {" +
                        "var selectedConfig = $(this).find('option').filter(':selected').val();" +
                        "if(selectedConfig == 'custom')" +
                        "{" +
                            "$('#" + divUserId + "').show();" +
                        "}" +
                        "else" +
                        "{" +
                            "$('#" + divUserId + "').hide();" +
                        "}" +
                    "}).change();"
                )
            );
        }
    }
}
