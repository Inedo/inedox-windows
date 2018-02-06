using System.ComponentModel;
using System.Threading;
using Inedo.BuildMaster.Data;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMasterExtensions.Windows.Legacy.ActionImporters;
using Inedo.Documentation;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.BuildMasterExtensions.Windows.Iis
{
    [DisplayName("Stop IIS App Pool")]
    [Description("Stops an application pool in IIS.")]
    [Tag("windows")]
    [Tag("iis")]
    [CustomEditor(typeof(StartStopIISAppActionEditor<ShutdownIisAppAction>))]
    [ConvertibleToOperation(typeof(StopAppPoolImporter))]
    public sealed class ShutdownIisAppAction : RemoteActionBase, IIISAppPoolAction
    {
        [Persistent]
        public string AppPool { get; set; }

        public override ExtendedRichDescription GetActionDescription()
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Stop ",
                    new Hilite(this.AppPool),
                    " application pool"
                )
            );
        }

        protected override void Execute()
        {
            this.LogDebug("Stopping application pool {0}...", this.AppPool);
            if (this.ExecuteRemoteCommand("stop") == Domains.YN.Yes)
                this.LogInformation("{0} application pool stopped.", this.AppPool);
        }
        protected override string ProcessRemoteCommand(string name, string[] args)
        {
            try
            {
                IISUtil.Instance.StopAppPool(this.AppPool);
                Thread.Sleep(100);
                return Domains.YN.Yes;
            }
            catch (IISException ex)
            {
                this.Log(ex.LogLevel, ex.Message);
                return Domains.YN.No;
            }
        }
    }
}
