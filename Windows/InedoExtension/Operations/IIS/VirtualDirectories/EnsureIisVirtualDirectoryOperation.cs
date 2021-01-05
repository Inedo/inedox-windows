using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Windows.Configurations.IIS;
using Microsoft.Web.Administration;

namespace Inedo.Extensions.Windows.Operations.IIS.VirtualDirectories
{
    [Serializable]
    [DisplayName("Ensure Virtual Directory")]
    [Description("Ensures the existence of a virtual directory within an IIS site.")]
    [ScriptAlias("Ensure-VirtualDirectory")]
    [ScriptNamespace(Namespaces.IIS)]
    [SeeAlso(typeof(Sites.EnsureIisSiteOperation))]
    [SeeAlso(typeof(Applications.EnsureIisApplicationOperation))]
    [Tag(Tags.IIS)]
    [Tag(Tags.Sites)]
    [Example(@"
# ensures that the hdars virtual directory pool is present on the web server
IIS::Ensure-VirtualDirectory(
    Site: Hdars,
    Path: /hdars,
    PhysicalPath: C:\hdars
);
")]
    public sealed class EnsureIisVirtualDirectoryOperation : EnsureOperation<IisVirtualDirectoryConfiguration>
    {
        public override Task<PersistedConfiguration> CollectAsync(IOperationCollectionContext context)
        {
            this.Template?.SetCredentialProperties(context as ICredentialResolutionContext);
            return EnsureVirtualDirectoryJob.CollectAsync<EnsureVirtualDirectoryJob>(this, context);
        }

        public override Task ConfigureAsync(IOperationExecutionContext context)
        {
            this.Template?.SetCredentialProperties(context as ICredentialResolutionContext);
            return EnsureVirtualDirectoryJob.EnsureAsync<EnsureVirtualDirectoryJob>(this, context);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            var shortDesc = new RichDescription(
                "Ensure ",
                new Hilite(config[nameof(IisVirtualDirectoryConfiguration.Path)]),
                " Virtual Directory");
            var longDesc = new RichDescription(
                "on site ",
                new Hilite(config[nameof(IisVirtualDirectoryConfiguration.SiteName)]));
            if (string.Equals(config[nameof(IisVirtualDirectoryConfiguration.Exists)], bool.FalseString, StringComparison.OrdinalIgnoreCase))
            {
                longDesc.AppendContent("does not exist");
                return new ExtendedRichDescription(shortDesc, longDesc);
            }

            longDesc.AppendContent(" at ", new DirectoryHilite(config[nameof(IisVirtualDirectoryConfiguration.PhysicalPath)]));

            var credential = config[nameof(IisVirtualDirectoryConfiguration.CredentialName)];
            var username = config[nameof(IisVirtualDirectoryConfiguration.UserName)];
            var logon = config[nameof(IisVirtualDirectoryConfiguration.LogonMethod)];

            if (!string.IsNullOrEmpty(credential))
                longDesc.AppendContent(" impersonate with credentials ", new Hilite(credential));
            else if (!string.IsNullOrEmpty(username))
                longDesc.AppendContent(" impersonate with username ", new Hilite(username));

            if (!string.IsNullOrEmpty(logon))
                longDesc.AppendContent(" (logon: ", new Hilite(logon), ")");

            return new ExtendedRichDescription(shortDesc, longDesc);
        }
        
        private sealed class EnsureVirtualDirectoryJob : SlimEnsureJob<IisVirtualDirectoryConfiguration>
        {
            public override Task<IisVirtualDirectoryConfiguration> CollectAsync(CancellationToken cancellationToken) => Task.FromResult(this.Collect());
            public override Task ConfigureAsync(CancellationToken cancellationToken)
            {
                this.Configure();
                return InedoLib.NullTask;
            }

            private IisVirtualDirectoryConfiguration Collect()
            {
                if (this.Template == null)
                    throw new InvalidOperationException("Template is not set.");

                this.LogDebug($"Looking for Virtual Directory \"{this.Template.FullPath}\"...");

                lock (Locks.IIS)
                {
                    using (var manager = new ServerManager())
                    {
                        var uninclused = new IisVirtualDirectoryConfiguration
                        {
                            Exists = false,
                            Path = this.Template.Path,
                            SiteName = this.Template.SiteName,
                            ApplicationPath = this.Template.ApplicationPath
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
                        var vdir = app.VirtualDirectories[this.Template.Path];
                        if (vdir == null)
                        {
                            this.LogInformation($"Virtual Directory \"{this.Template.Path}\" does not exist.");
                            return uninclused;
                        }

                        return IisVirtualDirectoryConfiguration.FromMwaVirtualDirectory(this.GetLogWrapper(), this.Template.SiteName, vdir, this.Template);
                    }
                }
            }
            private void Configure()
            {
                if (this.Template == null)
                    throw new InvalidOperationException("Template is not set.");

                lock (Locks.IIS)
                {
                    using (var manager = new ServerManager())
                    {
                        var site = manager.Sites[this.Template.SiteName];
                        if (site == null)
                        {
                            this.LogWarning($"Site \"{this.Template.SiteName}\" does not exist, cannot ensure a vdir on it.");
                            return;
                        }

                        var app = site.Applications[this.Template.ApplicationPath];
                        if (app == null)
                        {
                            this.LogWarning($"Application \"{this.Template.ApplicationPath}\" does not exist, cannot ensure a vdir on it.");
                            return;
                        }

                        var vdir = app.VirtualDirectories[this.Template.Path];
                        if (this.Template.Exists)
                        {
                            if (vdir == null)
                            {
                                this.LogDebug("Does not exist. Creating...");
                                if (!this.Simulation)
                                {
                                    vdir = app.VirtualDirectories.Add(this.Template.Path, this.Template.PhysicalPath);
                                    manager.CommitChanges();
                                }

                                this.LogInformation($"Virtual Directory \"{this.Template.FullPath}\" added.");
                                site = manager.Sites[this.Template.SiteName];
                                app = site.Applications[this.Template.ApplicationPath];
                                vdir = app.VirtualDirectories[this.Template.Path];
                            }

                            this.LogDebug("Applying configuration...");
                            if (!this.Simulation)
                                IisVirtualDirectoryConfiguration.SetMwaVirtualDirectory(this.GetLogWrapper(), this.Template, vdir);

                        }
                        else
                        {
                            if (vdir == null)
                            {
                                this.LogWarning("Virtual directory doesn't exist.");
                                return;
                            }

                            this.LogDebug("Exists. Deleting...");
                            if (!this.Simulation)
                                app.VirtualDirectories.Remove(vdir);
                        }

                        this.LogDebug("Committing configuration...");
                        if (!this.Simulation)
                            manager.CommitChanges();

                        this.LogInformation($"Virtual Directory \"{this.Template.FullPath}\" {(this.Template.Exists ? "configured" : "removed")}.");
                    }
                }
            }
        }
    }
}
