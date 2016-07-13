using System;
using System.ComponentModel;
using System.ServiceProcess;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Web;
using Inedo.Documentation;
using Inedo.Serialization;
using Inedo.WindowsServices;

namespace Inedo.BuildMasterExtensions.Windows.Services
{
    [DisplayName("Uninstall Service")]
    [Description("Uninstalls a Windows service.")]
    [Tag("windows")]
    [CustomEditor(typeof(UninstallServiceActionEditor))]
    public sealed class UninstallServiceAction : RemoteActionBase
    {
        [Persistent]
        public string ServiceName { get; set; }
        [Persistent]
        public bool ErrorIfNotInstalled { get; set; }
        [Persistent]
        public bool StopIfRunning { get; set; }

        public override ExtendedRichDescription GetActionDescription()
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Uninstall ",
                    new Hilite(this.ServiceName),
                    " Service"
                )
            );
        }

        protected override void Execute()
        {
            if (string.IsNullOrWhiteSpace(this.ServiceName))
            {
                this.LogError("Service name is required.");
                return;
            }

            this.ExecuteRemoteCommand("uninstall");
        }
        protected override string ProcessRemoteCommand(string name, string[] args)
        {
            using (var service = WindowsService.GetService(this.ServiceName))
            {
                if (service != null)
                {
                    if (this.StopIfRunning)
                    {
                        this.LogDebug("Determining if service needs to be stopped...");
                        try
                        {
                            using (var serviceController = new ServiceController(this.ServiceName))
                            {
                                if (serviceController.Status == ServiceControllerStatus.Running)
                                {
                                    this.LogDebug("Issuing service stop command...");
                                    try
                                    {
                                        serviceController.Stop();
                                        this.LogDebug("Service stop command issued.");
                                    }
                                    catch (Exception ex1)
                                    {
                                        this.LogWarning("Could not stop service: " + ex1.Message);
                                    }
                                }
                            }
                        }
                        catch (Exception ex2)
                        {
                            this.LogWarning("Could not determine if service is running: " + ex2.Message);
                        }
                    }

                    this.LogDebug("Uninstalling service {0}...", this.ServiceName);
                    try
                    {
                        service.Delete();
                        this.LogInformation("Service {0} uninstalled.", this.ServiceName);
                    }
                    catch (Exception ex)
                    {
                        this.LogError("Could not uninstall service: " + ex.Message);
                    }
                }
                else
                {
                    if (this.ErrorIfNotInstalled)
                        this.LogError("Service {0} was not found.", this.ServiceName);
                    else
                        this.LogInformation("Service {0} was not found.");
                }
            }

            return null;
        }
    }
}