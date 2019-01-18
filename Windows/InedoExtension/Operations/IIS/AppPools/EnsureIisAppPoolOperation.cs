using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Windows.Configurations.IIS;
using Microsoft.Web.Administration;

namespace Inedo.Extensions.Windows.Operations.IIS.AppPools
{
    [Serializable]
    [DisplayName("Ensure App Pool")]
    [Description("Ensures the existence of an application pool on a server.")]
    [ScriptAlias("Ensure-AppPool")]
    [ScriptNamespace(Namespaces.IIS)]
    [SeeAlso(typeof(Sites.EnsureIisSiteOperation))]
    [Tag(Tags.IIS)]
    [Tag(Tags.AppPools)]
    [Example(@"
# ensures that the Otter application pool is present on the web server
IIS::Ensure-AppPool(
    Name: OtterAppPool,
    Pipeline: 1, # classic mode
    Runtime: v4.0
);

# ensures that the DefaultAppPool is removed from the web server
IIS::Ensure-AppPool(
    Name: DefaultAppPool,
    Exists: false
);
")]
    public sealed class EnsureIisAppPoolOperation : RemoteEnsureOperation<IisAppPoolConfiguration>
    {
        private readonly static object syncLock = new object();

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            var shortDesc = new RichDescription("Ensure ", new Hilite(config[nameof(IisAppPoolConfiguration.Name)]), " Application Pool");
            if (string.Equals(config[nameof(IisAppPoolConfiguration.Exists)], bool.FalseString, StringComparison.OrdinalIgnoreCase))
                return new ExtendedRichDescription(shortDesc, new RichDescription("does not exist"));

            var otherProps = (from prop in typeof(IisAppPoolConfiguration).GetProperties()
                              where Attribute.IsDefined(prop, typeof(ScriptAliasAttribute))
                                 && prop.Name != nameof(IisAppPoolConfiguration.Name)
                                 && prop.Name != nameof(IisAppPoolConfiguration.Exists)
                                 && prop.Name != nameof(IisAppPoolConfiguration.Status)
                                 && prop.Name != nameof(IisAppPoolConfiguration.ManagedRuntimeVersion)
                                 && prop.Name != nameof(IisAppPoolConfiguration.ManagedPipelineMode)
                                 && prop.Name != nameof(IisAppPoolConfiguration.ProcessModel_IdentityType)
                                 && prop.Name != nameof(IisAppPoolConfiguration.ProcessModel_UserName)
                                 && prop.Name != nameof(IisAppPoolConfiguration.ProcessModel_Password)
                                 && prop.Name != nameof(IisAppPoolConfiguration.CredentialName)
                              let Value = config[prop.Name]
                              where !string.IsNullOrEmpty(Value)
                              select new
                              {
                                  prop.Name,
                                  DisplayName = prop.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? prop.Name,
                                  Value
                              }
                             ).ToList();

            var status = config[nameof(IisAppPoolConfiguration.Status)];
            var version = config[nameof(IisAppPoolConfiguration.ManagedRuntimeVersion)];
            var pipeline = config[nameof(IisAppPoolConfiguration.ManagedPipelineMode)];
            var credential = config[nameof(IisAppPoolConfiguration.CredentialName)];
            var username = config[nameof(IisAppPoolConfiguration.ProcessModel_UserName)];

            var longDesc = new RichDescription();
            bool longDescInclused = false;

            if (!string.IsNullOrEmpty(status))
            {
                longDesc.AppendContent("is ", new Hilite(status), " ");
                longDescInclused = true;
            }

            if (!string.IsNullOrEmpty(version))
            {
                longDesc.AppendContent("with .NET CLR ", new Hilite(version), " ");
                longDescInclused = true;
            }

            if (!string.IsNullOrEmpty(pipeline))
            {
                longDesc.AppendContent(new Hilite(pipeline), " pipeline ");
                longDescInclused = true;
            }

            if (!string.IsNullOrEmpty(credential))
            {
                longDesc.AppendContent("under credential ", new Hilite(credential), " ");
                longDescInclused = true;
            }
            else if (!string.IsNullOrEmpty(username))
            {
                longDesc.AppendContent("under user ", new Hilite(username), " ");
                longDescInclused = true;
            }

            var listParams = new List<string>();
            foreach (var prop in otherProps)
            {
                listParams.Add($"{prop.DisplayName}: {prop.Value}");
            }

            if (listParams.Any())
            {
                if (longDescInclused)
                    longDesc.AppendContent("and ");

                longDesc.AppendContent(new ListHilite(listParams));
            }

            return new ExtendedRichDescription(shortDesc, longDesc);
        }


        protected override Task<PersistedConfiguration> RemoteCollectAsync(IRemoteOperationCollectionContext context)
        {
            this.LogDebug($"Looking for Application Pool \"{this.Template.Name}\"...");

            lock (syncLock)
            {
                using (var manager = new ServerManager())
                {
                    var pool = manager.ApplicationPools[this.Template.Name];
                    if (pool == null)
                    {
                        this.LogInformation($"Application Pool \"{this.Template.Name}\" does not exist.");
                        return Task.FromResult<PersistedConfiguration>(new IisAppPoolConfiguration { Exists = false, Name = this.Template.Name });
                    }

                    return Task.FromResult<PersistedConfiguration>(IisAppPoolConfiguration.FromMwaApplicationPool(this, pool, this.Template));
                }
            }
        }

        protected override Task RemoteConfigureAsync(IRemoteOperationExecutionContext context)
        {
            if (this.Template == null)
                throw new InvalidOperationException("Template is not set.");

            this.LogDebug($"Looking for Application Pool \"{this.Template.Name}\"...");

            lock (syncLock)
            {
                using (var manager = new ServerManager())
                {
                    var pool = manager.ApplicationPools[this.Template.Name];
                    if (this.Template.Exists)
                    {
                        if (pool == null)
                        {
                            this.LogDebug("Does not exist. Creating...");
                            if (!context.Simulation)
                            {
                                pool = manager.ApplicationPools.Add(this.Template.Name);
                                manager.CommitChanges();
                            }

                            this.LogInformation($"Application Pool \"{this.Template.Name}\" added.");
                            this.LogDebug("Reloading configuration...");
                            pool = manager.ApplicationPools[this.Template.Name];
                        }

                        this.LogDebug("Applying configuration...");
                        if (!context.Simulation)
                        {
                            IisAppPoolConfiguration.SetMwaApplicationPool(this, this.Template, pool);
                            manager.CommitChanges();
                        }

                        if (this.Template.Status.HasValue)
                        {
                            this.LogDebug("Reloading configuration...");
                            pool = manager.ApplicationPools[this.Template.Name];

                            if (this.Template.Status.Value == IisObjectState.Started && pool.State == ObjectState.Stopped)
                            {
                                this.LogDebug($"Starting application pool...");
                                if (!context.Simulation)
                                    pool.Start();
                            }
                            else if (this.Template.Status.Value == IisObjectState.Stopped && pool.State == ObjectState.Started)
                            {
                                this.LogDebug($"Stopping application pool...");
                                if (!context.Simulation)
                                    pool.Stop();
                            }
                            else
                            {
                                this.LogDebug($"State not changed to {this.Template.Status.Value} because state is currently {pool.State}.");
                            }
                        }
                    }
                    else
                    {
                        if (pool == null)
                        {
                            this.LogWarning("Application pool doesn't exist.");
                            return Complete();
                        }

                        this.LogDebug("Exists. Deleting...");
                        if (!context.Simulation)
                            manager.ApplicationPools.Remove(pool);
                    }

                    this.LogDebug("Committing configuration...");
                    if (!context.Simulation)
                        manager.CommitChanges();

                    this.LogInformation($"Application Pool \"{this.Template.Name}\" {(this.Template.Exists ? "configured" : "removed")}.");
                }
            }

            return Complete();
        }
    }
}
