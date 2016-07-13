using System;
using System.ComponentModel;
using System.ServiceProcess;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Web;
using Inedo.BuildMasterExtensions.Windows.ActionImporters;
using Inedo.Documentation;
using Inedo.Serialization;

namespace Inedo.BuildMasterExtensions.Windows.Services
{
    [DisplayName("Stop Service")]
    [Description("Stops a Windows service.")]
    [Tag("windows")]
    [CustomEditor(typeof(StopServiceActionEditor))]
    [ConvertibleToOperation(typeof(StopServiceImporter))]
    public sealed class StopServiceAction : RemoteActionBase
    {
        [Persistent]
        public string ServiceName { get; set; }
        [Persistent]
        public bool WaitForStop { get; set; } = true;
        [Persistent]
        public bool IgnoreAlreadyStoppedError { get; set; }

        public override ExtendedRichDescription GetActionDescription()
        {
            var longDesc = new RichDescription();

            return new ExtendedRichDescription(
                new RichDescription(
                    "Stop ",
                    this.ServiceName,
                    " Service"
                ),
                longDesc
            );
        }

        protected override void Execute()
        {
            this.LogInformation("Stopping service {0}...", this.ServiceName);
            this.ExecuteRemoteCommand("stop");
        }
        protected override string ProcessRemoteCommand(string name, string[] args)
        {
            using (var sc = new ServiceController(this.ServiceName))
            {
                if (sc.Status == ServiceControllerStatus.Stopped || sc.Status == ServiceControllerStatus.StopPending)
                {
                    if (this.IgnoreAlreadyStoppedError)
                        this.LogInformation("Service is already stopped.");
                    else
                        this.LogError("Service is already stopped.");

                    return null;
                }

                try
                {
                    sc.Stop();
                }
                catch (Exception ex)
                {
                    this.LogError("Service could not be stopped: " + ex.Message);
                    return null;
                }

                if (this.WaitForStop)
                {
                    this.LogInformation("Waiting for service to stop...");
                    bool stopped = false;
                    while (!stopped)
                    {
                        sc.Refresh();
                        stopped = sc.Status == ServiceControllerStatus.Stopped;
                        if (stopped)
                            break;

                        this.Context.CancellationToken.WaitHandle.WaitOne(1000 * 3);
                        this.ThrowIfCanceledOrTimeoutExpired();
                    }

                    this.LogInformation("Service stopped.");
                }
                else
                {
                    this.LogInformation("Service ordered to stop.");
                }
            }

            return null;
        }
    }
}
