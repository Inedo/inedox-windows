using System.ComponentModel;
using System.ServiceProcess;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Windows.Operations.Services
{
    [DisplayName("Stop Windows Service")]
    [Description("Stops an existing Windows service.")]
    [DefaultProperty(nameof(ServiceName))]
    [ScriptAlias("Stop-Service")]
    [Tag(Tags.Services)]
    [Example(@"# stops the HDARS service on the remote server
Stop-Service HDARS;")]
    [ScriptNamespace(Namespaces.Windows, PreferUnqualified = true)]
    public sealed class StopServiceOperation : ExecuteOperation
    {
        [Required]
        [ScriptAlias("Name")]
        [DisplayName("Service name")]
        public string ServiceName { get; set; }
        [ScriptAlias("WaitForStoppedStatus")]
        [DisplayName("Wait for stopped status")]
        [DefaultValue(true)]
        public bool WaitForStoppedStatus { get; set; } = true;

        public override Task ExecuteAsync(IOperationExecutionContext context)
        {
            this.LogInformation($"Stopping service {this.ServiceName}...");
            if (context.Simulation)
            {
                this.LogInformation("Service is stopped.");
                return Complete;
            }

            var jobExecuter = context.Agent.GetService<IRemoteJobExecuter>();
            var job = new ControlServiceJob { ServiceName = this.ServiceName, TargetStatus = ServiceControllerStatus.Stopped, WaitForTargetStatus = this.WaitForStoppedStatus };
            job.MessageLogged += (s, e) => this.Log(e.Level, e.Message);
            return jobExecuter.ExecuteJobAsync(job, context.CancellationToken);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Stop ",
                    new Hilite(config[nameof(ServiceName)]),
                    " service"
                )
            );
        }
    }
}
