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
        [DisplayName("Site name")]
        [Description("The name of the IIS site to operate on.")]
        public string SiteName { get; set; }

        internal abstract SiteOperationType OperationType { get; }

        public override sealed async Task ExecuteAsync(IOperationExecutionContext context)
        {
            if (context.Simulation)
            {
                switch (this.OperationType)
                {
                    case SiteOperationType.Start:
                        this.LogInformation($"Starting site {this.SiteName}...");
                        this.LogInformation($"Site {this.SiteName} state is now Started.");
                        break;
                    case SiteOperationType.Stop:
                        this.LogInformation($"Stopping site {this.SiteName}...");
                        this.LogInformation($"Site {this.SiteName} state is now Stopped.");
                        break;
                }
            }
            else
            {
                var job = new SiteJob
                {
                    SiteName = this.SiteName,
                    OperationType = this.OperationType
                };

                job.MessageLogged += (s, e) => this.Log(e.Level, e.Message);

                var jobExecuter = context.Agent.GetService<IRemoteJobExecuter>();
                await jobExecuter.ExecuteJobAsync(job, context.CancellationToken);
            }
        }
    }
}
