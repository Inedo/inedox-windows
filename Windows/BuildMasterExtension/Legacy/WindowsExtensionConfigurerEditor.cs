using System.Web.UI.WebControls;
using Inedo.BuildMaster.Extensibility.Configurers.Extension;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.BuildMasterExtensions.Windows
{
    internal sealed class WindowsExtensionConfigurerEditor : ExtensionConfigurerEditorBase
    {
        private CheckBox chkOverridePowerShellDefaults;

        public override void InitializeDefaultValues()
        {
        }
        public override void BindToForm(ExtensionConfigurerBase extension)
        {
            var configurer = (WindowsExtensionConfigurer)extension;

            this.chkOverridePowerShellDefaults.Checked = configurer.OverridePowerShellDefaults;
        }
        public override ExtensionConfigurerBase CreateFromForm()
        {
            return new WindowsExtensionConfigurer
            {
                OverridePowerShellDefaults = this.chkOverridePowerShellDefaults.Checked
            };
        }

        protected override void CreateChildControls()
        {
            this.chkOverridePowerShellDefaults = new CheckBox
            {
                Text = "Override PowerShell script defaults"
            };

            this.Controls.Add(
                new SlimFormField(
                    "Options:",
                    this.chkOverridePowerShellDefaults,
                    new P(
                        "When checked, default values for PowerShell script parameters defined in BuildMaster will always be passed in, regardless of any defaults defined within the script itself."
                    ) { IsIdRequired = false, Class = "subLabel" }
                )
            );
        }
    }
}
