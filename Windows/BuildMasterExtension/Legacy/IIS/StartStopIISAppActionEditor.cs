using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;

namespace Inedo.BuildMasterExtensions.Windows.Iis
{
    internal sealed class StartStopIISAppActionEditor<TAction> : ActionEditorBase
        where TAction : ActionBase, IIISAppPoolAction, new()
    {
        private ValidatingTextBox txtAppPool;

        public override void BindToForm(ActionBase extension)
        {
            var action = (TAction)extension;
            this.txtAppPool.Text = action.AppPool;
        }
        public override ActionBase CreateFromForm()
        {
            return new TAction
            {
                AppPool = this.txtAppPool.Text
            };
        }

        protected override void CreateChildControls()
        {
            this.txtAppPool = new ValidatingTextBox { Required = true };

            this.Controls.Add(
                new SlimFormField("Application pool:", this.txtAppPool)
            );
        }
    }
}
