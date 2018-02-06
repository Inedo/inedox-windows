using System;
using System.ComponentModel;
using System.ServiceProcess;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMasterExtensions.Windows.ActionImporters;
using Inedo.Documentation;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.BuildMasterExtensions.Windows.Services
{
    [DisplayName("Start Service")]
    [Description("Starts a Windows Service.")]
    [Tag("windows")]
    [CustomEditor(typeof(StartServiceActionEditor))]
    [ConvertibleToOperation(typeof(StartServiceImporter))]
    public sealed class StartServiceAction : RemoteActionBase
    {
        [Persistent]
        public string ServiceName { get; set; }

        [Persistent]
        public string[] StartupArgs { get; set; }

        [Persistent]
        public bool WaitForStart { get; set; } = true;

        [Persistent]
        public bool IgnoreAlreadyStartedError { get; set; }

        [Persistent]
        public bool TreatUnableToStartAsWarning { get; set; }

        public override ExtendedRichDescription GetActionDescription()
        {
            var longDesc = new RichDescription();
            if (this.StartupArgs != null && this.StartupArgs.Length > 0)
            {
                longDesc.AppendContent(
                    "with arguments: ",
                    new Hilite(string.Join(" ", this.StartupArgs))
                );
            }

            return new ExtendedRichDescription(
                new RichDescription(
                    "Start ",
                    this.ServiceName,
                    " Service"
                ),
                longDesc
            );
        }

        protected override void Execute()
        {
            this.LogInformation("Starting service {0}...", this.ServiceName);
            this.ExecuteRemoteCommand("start");
        }
        protected override string ProcessRemoteCommand(string name, string[] args)
        {
            using (var sc = new ServiceController(this.ServiceName))
            {
                if (sc.Status == ServiceControllerStatus.Running || sc.Status == ServiceControllerStatus.StartPending)
                {
                    if (this.IgnoreAlreadyStartedError)
                        this.LogInformation("Service is already running.");
                    else
                        this.LogError("Service is already running.");

                    return null;
                }

                try
                {
                    sc.Start(this.StartupArgs ?? new string[0]);
                }
                catch (Exception ex)
                {
                    this.LogErrorWarning("Service could not be started: " + ex.Message);
                    return null;
                }

                if (this.WaitForStart)
                {
                    this.LogInformation("Waiting for service to start...");
                    bool started = false;
                    while (!started)
                    {
                        sc.Refresh();
                        started = sc.Status == ServiceControllerStatus.Running;
                        if (started)
                            break;

                        this.Context.CancellationToken.WaitHandle.WaitOne(1000 * 3);
                        this.ThrowIfCanceledOrTimeoutExpired();

                        if (sc.Status == ServiceControllerStatus.Stopped)
                        {
                            this.LogErrorWarning("Service stopped immediately after starting.");
                            return null;
                        }
                    }

                    this.LogInformation("Service started.");
                }
                else
                {
                    this.LogInformation("Service ordered to start.");
                }
            }

            return null;
        }

        private void LogErrorWarning(string message)
        {
            if (this.TreatUnableToStartAsWarning)
                this.LogWarning(message);
            else
                this.LogError(message);
        }
    }
}
