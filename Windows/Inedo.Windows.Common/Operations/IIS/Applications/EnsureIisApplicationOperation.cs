using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Configurations;
using Inedo.Otter.Extensibility.Operations;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Configurations;
using Inedo.BuildMaster.Extensibility.Operations;
#endif
using Inedo.Extensions.Windows.Configurations.IIS;
using Microsoft.Web.Administration;

namespace Inedo.Extensions.Windows.Operations.IIS.Applications
{
    [Serializable]
    [DisplayName("Ensure Application")]
    [Description("Ensures the existence of an application within an IIS site.")]
    [ScriptAlias("Ensure-Application")]
    [ScriptNamespace(Namespaces.IIS)]
    [SeeAlso(typeof(Sites.EnsureIisSiteOperation))]
    [SeeAlso(typeof(VirtualDirectories.EnsureIisVirtualDirectoryOperation))]
    [Tag(Tags.IIS)]
    [Tag(Tags.Sites)]
    [Example(@"
# ensures that the hdars application is present on the web server
IIS::Ensure-Application(
    Site: Hdars,
    Path: /hdars,
    PhysicalPath: C:\hdars
);
")]
    public sealed class EnsureIisApplicationOperation : RemoteEnsureOperation<IisApplicationConfiguration>
    {
        private readonly static object lockbox = new object();

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            var shortDesc = new RichDescription(
                "Ensure ",
                new Hilite(config[nameof(IisApplicationConfiguration.ApplicationPath)]),
                " Application");
            var longDesc = new RichDescription(
                "on site ",
                new Hilite(config[nameof(IisApplicationConfiguration.SiteName)]));
            if (string.Equals(config[nameof(IisApplicationConfiguration.Exists)], bool.FalseString, StringComparison.OrdinalIgnoreCase))
            {
                longDesc.AppendContent("does not exist");
                return new ExtendedRichDescription(shortDesc, longDesc);
            }

            return new ExtendedRichDescription(shortDesc, longDesc);
        }

#if Otter
        protected override Task<PersistedConfiguration> RemoteCollectAsync(IRemoteOperationExecutionContext context)
        {
            if (this.Template == null)
                throw new InvalidOperationException("Template is not set.");

            this.LogDebug($"Looking for Application \"{this.Template.ApplicationPath}\"...");

            lock (lockbox)
                using (var manager = new ServerManager())
                {
                    var uninclused = new IisApplicationConfiguration
                    {
                        Exists = false,
                        ApplicationPath = this.Template.ApplicationPath,
                        SiteName = this.Template.SiteName
                    };

                    var site = manager.Sites[this.Template.SiteName];
                    if (site == null)
                    {
                        this.LogInformation($"Site \"{this.Template.SiteName}\" does not exist.");
                        return Complete(uninclused);
                    }
                    var app = site.Applications[this.Template.ApplicationPath];
                    if (app == null)
                    {
                        this.LogInformation($"Application \"{this.Template.ApplicationPath}\" does not exist.");
                        return Complete(uninclused);
                    }

                    return Complete(IisApplicationConfiguration.FromMwaApplication(this, this.Template.SiteName, app, this.Template));
                }
        }
#endif

        protected override Task RemoteConfigureAsync(IRemoteOperationExecutionContext context)
        {
            if (this.Template == null)
                throw new InvalidOperationException("Template is not set.");

            lock (lockbox)
                using (var manager = new ServerManager())
                {
                    var site = manager.Sites[this.Template.SiteName];
                    if (site == null)
                    {
                        this.LogWarning($"Site \"{this.Template.SiteName}\" does not exist, cannot ensure an application on it.");
                        return Complete();
                    }
                    var app = site.Applications[this.Template.ApplicationPath];
                    if (this.Template.Exists)
                    {
                        if (app == null)
                        {
                            this.LogDebug("Does not exist. Creating...");
                            if (!context.Simulation)
                            {
                                app = site.Applications.Add(this.Template.ApplicationPath, this.Template.PhysicalPath);
                                manager.CommitChanges();
                            }

                            this.LogInformation($"Application \"{this.Template.ApplicationPath}\" added.");
                            app = site.Applications[this.Template.ApplicationPath];
                        }

                        this.LogDebug("Applying configuration...");
                        if (!context.Simulation)
                            IisApplicationConfiguration.SetMwaApplication(this, this.Template, app);

                    }
                    else
                    {
                        if (app == null)
                        {
                            this.LogWarning("Application doesn't exist.");
                            return Complete();
                        }

                        this.LogDebug("Exists. Deleting...");
                        if (!context.Simulation)
                            site.Applications.Remove(app);
                    }

                    this.LogDebug("Committing configuration...");
                    if (!context.Simulation)
                        manager.CommitChanges();

                    this.LogInformation($"Application \"{this.Template.ApplicationPath}\" {(this.Template.Exists ? "configured" : "removed")}.");
                }

            return Complete();
        }
    }
}
