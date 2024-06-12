using System.ComponentModel;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
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
    [Example("""
        # ensures that the hdars application is present on the web server
        IIS::Ensure-Application(
            Site: Hdars,
            Path: /hdars,
            PhysicalPath: C:\hdars
        );
        """)]
    public sealed class EnsureIisApplicationOperation : EnsureOperation<IisApplicationConfiguration>
    {
        public override Task<PersistedConfiguration> CollectAsync(IOperationCollectionContext context) 
        {
            this.Template.SetCredentialProperties(context as ICredentialResolutionContext);
            return EnsureApplicationJob.CollectAsync<EnsureApplicationJob>(this, context);
        }
        public override Task ConfigureAsync(IOperationExecutionContext context) 
        {
            this.Template.SetCredentialProperties(context as ICredentialResolutionContext);
            return EnsureApplicationJob.EnsureAsync<EnsureApplicationJob>(this, context);
        }

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

        private sealed class EnsureApplicationJob : SlimEnsureJob<IisApplicationConfiguration>
        {
            public override Task<IisApplicationConfiguration> CollectAsync(CancellationToken cancellationToken) => Task.FromResult(this.Collect());
            public override Task ConfigureAsync(CancellationToken cancellationToken)
            {
                this.Configure();
                return Task.CompletedTask;
            }

            private IisApplicationConfiguration Collect()
            {
                if (this.Template == null)
                    throw new InvalidOperationException("Template is not set.");

                this.LogDebug($"Looking for Application \"{this.Template.ApplicationPath}\"...");

                lock (Locks.IIS)
                {
                    using var manager = new ServerManager();
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
                        return uninclused;
                    }
                    var app = site.Applications[this.Template.ApplicationPath];
                    if (app == null)
                    {
                        this.LogInformation($"Application \"{this.Template.ApplicationPath}\" does not exist.");
                        return uninclused;
                    }

                    return IisApplicationConfiguration.FromMwaApplication(this.GetLogWrapper(), this.Template.SiteName, app, this.Template);
                }
            }
            private void Configure()
            {
                if (this.Template == null)
                    throw new InvalidOperationException("Template is not set.");

                lock (Locks.IIS)
                {
                    using var manager = new ServerManager();
                    var site = manager.Sites[this.Template.SiteName];
                    if (site == null)
                    {
                        if (this.Template.Exists)
                            this.LogWarning($"Site \"{this.Template.SiteName}\" does not exist, cannot ensure an application on it.");

                        return;
                    }
                    var app = site.Applications[this.Template.ApplicationPath];
                    if (this.Template.Exists)
                    {
                        if (app == null)
                        {
                            this.LogDebug("Does not exist. Creating...");
                            if (!this.Simulation)
                            {
                                app = site.Applications.Add(this.Template.ApplicationPath, this.Template.PhysicalPath);
                                manager.CommitChanges();
                            }

                            this.LogInformation($"Application \"{this.Template.ApplicationPath}\" added.");
                            site = manager.Sites[this.Template.SiteName];
                            app = site.Applications[this.Template.ApplicationPath];
                        }

                        this.LogDebug("Applying configuration...");
                        if (!this.Simulation)
                            IisApplicationConfiguration.SetMwaApplication(this.GetLogWrapper(), this.Template, app);

                    }
                    else
                    {
                        if (app == null)
                        {
                            this.LogWarning("Application doesn't exist.");
                            return;
                        }

                        this.LogDebug("Exists. Deleting...");
                        if (!this.Simulation)
                            site.Applications.Remove(app);
                    }

                    this.LogDebug("Committing configuration...");
                    if (!this.Simulation)
                        manager.CommitChanges();

                    this.LogInformation($"Application \"{this.Template.ApplicationPath}\" {(this.Template.Exists ? "configured" : "removed")}.");
                }
            }
        }
    }
}
