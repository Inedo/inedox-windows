using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensions.Windows.PowerShell;
using Inedo.Otter.Data;
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Configurations;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Extensions.Configurations;
using Inedo.Serialization;
using System.Management.Automation;

namespace Inedo.Extensions.Windows.Operations.PowerShell
{
    [DisplayName("Collect DSC Modules")]
    [Description("Collects the names and versions of DSC modules installed on a server.")]
    [ScriptAlias("Collect-DscModules")]
    [Tag(Tags.PowerShell)]
    [ScriptNamespace(Namespaces.PowerShell, PreferUnqualified = true)]
    public sealed class CollectDscModulesOperation : CollectOperation<DictionaryConfiguration>
    {
        public async override Task<DictionaryConfiguration> CollectConfigAsync(IOperationExecutionContext context)
        {
            var job = new CollectDscModulesJob()
            {
                DebugLogging = true
            };

            job.MessageLogged += (s, e) => this.Log(e.Level, e.Message);

            var jobExecuter = await context.Agent.GetServiceAsync<IRemoteJobExecuter>().ConfigureAwait(false);
            var result = (CollectDscModulesJob.Result)await jobExecuter.ExecuteJobAsync(job, context.CancellationToken).ConfigureAwait(false);
            
            using (var db = new DB.Context())
            {
                await db.ServerPackages_DeletePackagesAsync(
                    Server_Id: context.ServerId,
                    PackageType_Name: "DSC Module"
                ).ConfigureAwait(false);

                foreach (var module in result.Modules)
                {
                    await db.ServerPackages_CreateOrUpdatePackageAsync(
                        Server_Id: context.ServerId,
                        PackageType_Name: "DSC Module",
                        Package_Name: module.Name,
                        Package_Version: module.Version,
                        CollectedOn_Execution_Id: context.ExecutionId,
                        Url_Text: null,
                        CollectedFor_ServerRole_Id: context.ServerRoleId
                    ).ConfigureAwait(false);
                }
            }

            return null;
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Collect PowerShell DSC Modules")
            );
        }
    }
}
