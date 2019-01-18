using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Windows.Operations.IIS.Sites
{
    [Tag(Tags.IIS)]
    [Tag(Tags.Sites)]
    [DefaultProperty(nameof(SiteName))]
    [Example(@"
# stops the BuildMaster web site 
IIS::Stop-Site BuildMaster;

# starts the BuildMaster web site
IIS::Start-Site BuildMaster;
")]
    public abstract class SiteOperationBase : ExecuteOperation
    {
        internal SiteOperationBase()
        {
        }

        [Required]
        [ScriptAlias("Name")]
        [DisplayName("Site")]
        [Description("The name of the IIS site to operate on.")]
        public string SiteName { get; set; }

        public abstract bool WaitForTargetStatus { get; set; }

        internal abstract SiteOperationType OperationType { get; }

        public override sealed async Task ExecuteAsync(IOperationExecutionContext context)
        {
            if (context.Simulation)
            {
                switch (this.OperationType)
                {
                    case SiteOperationType.Start:
                        this.LogInformation($"Starting site {this.SiteName}...");
                        this.LogInformation($"Site {this.SiteName} state is now started.");
                        break;
                    case SiteOperationType.Stop:
                        this.LogInformation($"Stopping site {this.SiteName}...");
                        this.LogInformation($"Site {this.SiteName} state is now stopped.");
                        break;
                }
            }
            else
            {
                var job = new SiteJob
                {
                    SiteName = this.SiteName,
                    OperationType = this.OperationType,
                    WaitForTargetStatus = this.WaitForTargetStatus
                };

                job.MessageLogged += (s, e) => this.Log(e.Level, e.Message);

                var jobExecuter = await context.Agent.GetServiceAsync<IRemoteJobExecuter>();
                await jobExecuter.ExecuteJobAsync(job, context.CancellationToken);
            }
        }
    }
}
