using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Windows.Configurations.DSC;
using Inedo.Extensions.Windows.PowerShell;

namespace Inedo.Extensions.Windows.Operations.PowerShell
{
    [DisplayName("Collect DSC Modules")]
    [Description("Collects the names and versions of DSC modules installed on a server.")]
    [ScriptAlias("Collect-DscModules")]
    [Tag(Tags.PowerShell)]
    [ScriptNamespace(Namespaces.PowerShell, PreferUnqualified = true)]
    public sealed class CollectDscModulesOperation : CollectOperation<DscConfiguration>
    {
        public async override Task<DscConfiguration> CollectConfigAsync(IOperationCollectionContext context)
        {
            var job = new CollectDscModulesJob
            {
                DebugLogging = true
            };

            job.MessageLogged += (s, e) => this.Log(e.Level, e.Message);

            var jobExecuter = await context.Agent.GetServiceAsync<IRemoteJobExecuter>();
            var result = (CollectDscModulesJob.Result)await jobExecuter.ExecuteJobAsync(job, context.CancellationToken);

            using (var serverContext = context.GetServerCollectionContext())
            {
                await serverContext.ClearAllPackagesAsync("DSC Module");

                foreach (var module in result.Modules)
                {
                    await serverContext.CreateOrUpdatePackageAsync(
                        packageType: "DSC Module",
                        packageName: module.Name,
                        packageVersion: module.Version,
                        packageUrl: null
                    );
                }

                return null;
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Collect PowerShell DSC Modules")
            );
        }
    }
}
