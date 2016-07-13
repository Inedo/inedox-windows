using System.ComponentModel;
using System.IO;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Data;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Web;
using Inedo.Documentation;
using Inedo.Serialization;
using Inedo.WindowsServices;

namespace Inedo.BuildMasterExtensions.Windows.Services
{
    [DisplayName("Install Service")]
    [Description("Installs a Windows service.")]
    [Tag("windows")]
    [CustomEditor(typeof(InstallServiceActionEditor))]
    public sealed class InstallServiceAction : RemoteActionBase
    {
        [Persistent]
        public string ExePath { get; set; }
        [Persistent]
        public string Arguments { get; set; }
        [Persistent]
        public string ServiceName { get; set; }
        [Persistent]
        public string ServiceDisplayName { get; set; }
        [Persistent]
        public string UserAccount { get; set; }
        [Persistent]
        public string Password { get; set; }
        [Persistent]
        public string ServiceDescription { get; set; }
        [Persistent]
        public bool Recreate { get; set; }
        [Persistent]
        public bool ErrorIfAlreadyInstalled { get; set; }

        public override ExtendedRichDescription GetActionDescription()
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Install ",
                    new Hilite(this.ServiceName),
                    " Service"
                ),
                new RichDescription(
                    "from ",
                    new Hilite(this.ExePath + " " + this.Arguments)
                )
            );
        }

        protected override void Execute()
        {
            if (string.IsNullOrWhiteSpace(this.ExePath))
            {
                this.LogError("Service executable path is required.");
                return;
            }

            if (string.IsNullOrWhiteSpace(this.ServiceName))
            {
                this.LogError("Service name is required.");
                return;
            }

            if (string.IsNullOrWhiteSpace(this.ServiceDisplayName))
            {
                this.LogError("Service display name is required.");
                return;
            }

            this.ExecuteRemoteCommand("install");
        }
        protected override string ProcessRemoteCommand(string name, string[] args)
        {
            if (!File.Exists(this.ExePath))
                this.LogWarning("{0} does not exist; service may not start.", this.ExePath);

            using (var service = WindowsService.GetService(this.ServiceName))
            {
                if (service != null)
                {
                    if (this.ErrorIfAlreadyInstalled)
                    {
                        this.LogError("Service is already installed.");
                        return Domains.YN.No;
                    }

                    if (!this.Recreate)
                    {
                        this.LogWarning("Service is already installed.");
                        return Domains.YN.No;
                    }

                    this.LogDebug("Service is already installed; deleting...");
                    service.Delete();
                    this.LogDebug("Service deleted.");
                }
            }

            var fullExePath = "\"" + this.ExePath + "\"";
            if (!string.IsNullOrWhiteSpace(this.Arguments))
                fullExePath += " " + this.Arguments;

            this.LogDebug("Service Path: " + fullExePath);

            this.LogDebug("Creating service {0}...", this.ServiceName);
            using (var service = WindowsService.CreateService(this.ServiceName, this.ServiceDisplayName, fullExePath, Util.NullIf(this.UserAccount, string.Empty), Util.NullIf(this.Password, string.Empty)))
            {
                if (!string.IsNullOrWhiteSpace(this.ServiceDescription))
                {
                    this.LogDebug("Setting service description to " + this.ServiceDescription);
                    service.Description = this.ServiceDescription;
                }
            }

            this.LogInformation("Service {0} installed.", this.ServiceName);
            return Domains.YN.Yes;
        }
    }
}
