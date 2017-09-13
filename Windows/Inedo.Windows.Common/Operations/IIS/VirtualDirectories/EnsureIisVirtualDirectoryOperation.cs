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
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
#endif
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
    public sealed class EnsureIisVirtualDirectoryOperation : RemoteEnsureOperation<IisVirtualDirectoryConfiguration>
    {
        private readonly static object lockbox = new object();

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

#if Otter
        protected override Task<PersistedConfiguration> RemoteCollectAsync(IRemoteOperationExecutionContext context)
        {
            if (this.Template == null)
                throw new InvalidOperationException("Template is not set.");

            this.LogDebug($"Looking for Virtual Directory \"{this.Template.FullPath}\"...");

            lock (lockbox)
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
                        return Complete(uninclused);
                    }
                    var app = site.Applications[this.Template.ApplicationPath];
                    if (app == null)
                    {
                        this.LogInformation($"Application \"{this.Template.ApplicationPath}\" does not exist.");
                        return Complete(uninclused);
                    }
                    var vdir = app.VirtualDirectories[this.Template.Path];
                    if (vdir == null)
                    {
                        this.LogInformation($"Virtual Directory \"{this.Template.Path}\" does not exist.");
                        return Complete(uninclused);
                    }

                    return Complete(IisVirtualDirectoryConfiguration.FromMwaVirtualDirectory(this, this.Template.SiteName, vdir, this.Template));
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
                        this.LogWarning($"Site \"{this.Template.SiteName}\" does not exist, cannot ensure a vdir on it.");
                        return Complete();
                    }
                    var app = site.Applications[this.Template.ApplicationPath];
                    if (app == null)
                    {
                        this.LogWarning($"Application \"{this.Template.ApplicationPath}\" does not exist, cannot ensure a vdir on it.");
                        return Complete();
                    }

                    var vdir = app.VirtualDirectories[this.Template.Path];
                    if (this.Template.Exists)
                    {
                        if (vdir == null)
                        {
                            this.LogDebug("Does not exist. Creating...");
                            if (!context.Simulation)
                            {
                                vdir = app.VirtualDirectories.Add(this.Template.Path, this.Template.PhysicalPath);
                                manager.CommitChanges();
                            }

                            this.LogInformation($"Virtual Directory \"{this.Template.FullPath}\" added.");
                            vdir = app.VirtualDirectories[this.Template.Path];
                        }

                        this.LogDebug("Applying configuration...");
                        if (!context.Simulation)
                            IisVirtualDirectoryConfiguration.SetMwaVirtualDirectory(this, this.Template, vdir);

                    }
                    else
                    {
                        if (vdir == null)
                        {
                            this.LogWarning("Virtual directory doesn't exist.");
                            return Complete();
                        }

                        this.LogDebug("Exists. Deleting...");
                        if (!context.Simulation)
                            app.VirtualDirectories.Remove(vdir);
                    }

                    this.LogDebug("Committing configuration...");
                    if (!context.Simulation)
                        manager.CommitChanges();

                    this.LogInformation($"Virtual Directory \"{this.Template.FullPath}\" {(this.Template.Exists ? "configured" : "removed")}.");
                }

            return Complete();
        }
    }
}
