using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Windows.Operations.IIS.AppPools
{
    [Tag(Tags.IIS)]
    [Tag(Tags.ApplicationPools)]
    [DefaultProperty(nameof(ApplicationPoolName))]
    [Example(@"
# stops the BuildMaster application pool 
IIS::Stop-AppPool BuildMasterAppPool;

# starts the BuildMaster application pool 
IIS::Start-AppPool BuildMasterAppPool;

# recycles the BuildMaster application pool 
IIS::Recycle-AppPool BuildMasterAppPool;
")]
    public abstract class AppPoolOperationBase : ExecuteOperation
    {
        internal AppPoolOperationBase()
        {
        }

        [Required]
        [ScriptAlias("Name")]
        [Description("The name of the application pool to operate on.")]
        public string ApplicationPoolName { get; set; }

        internal abstract AppPoolOperationType OperationType { get; }

        public override sealed Task ExecuteAsync(IOperationExecutionContext context)
        {
            if (context.Simulation)
            {
                switch (this.OperationType)
                {
                    case AppPoolOperationType.Start:
                        this.LogInformation($"Starting application pool {this.ApplicationPoolName}...");
                        this.LogInformation($"Application pool {this.ApplicationPoolName} state is now Started.");
                        break;
                    case AppPoolOperationType.Stop:
                        this.LogInformation($"Stopping application pool {this.ApplicationPoolName}...");
                        this.LogInformation($"Application pool {this.ApplicationPoolName} state is now Stopped.");
                        break;
                    case AppPoolOperationType.Recycle:
                        this.LogInformation($"Recycling application pool {this.ApplicationPoolName}...");
                        this.LogInformation($"Application pool {this.ApplicationPoolName} state is now Started.");
                        break;
                }

                return Complete;
            }
            else
            {
                var job = new AppPoolJob
                {
                    AppPoolName = this.ApplicationPoolName,
                    OperationType = this.OperationType
                };

                job.MessageLogged += (s, e) => this.Log(e.Level, e.Message);

                var jobExecuter = context.Agent.GetService<IRemoteJobExecuter>();
                return jobExecuter.ExecuteJobAsync(job, context.CancellationToken);
            }
        }
    }
}
