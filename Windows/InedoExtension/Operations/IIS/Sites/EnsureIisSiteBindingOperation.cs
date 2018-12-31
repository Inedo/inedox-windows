using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Windows.Configurations.IIS;
using Microsoft.Web.Administration;

namespace Inedo.Extensions.Windows.Operations.IIS.Sites
{
    [Serializable]
    [DisplayName("Ensure Site Binding")]
    [Description("Ensures the existence of a binding on a site.")]
    [ScriptAlias("Ensure-SiteBinding")]
    [Tag(Tags.IIS)]
    [ScriptNamespace(Namespaces.IIS)]
    [Tag(Tags.Sites)]
    [SeeAlso(typeof(EnsureIisSiteOperation))]
    [Example(@"
# ensures that the Otter web site is present on the web server, and binds the site to the single IP address 192.0.2.100 on port 80 and hostname ""example.com""
IIS::Ensure-Site(
    Name: Otter,
    AppPool: OtterAppPool,
    Path: E:\Websites\Otter
);

IIS::Ensure-SiteBinding(
    Site: Otter,
    Protocol: http,
    Address: 192.0.2.100,
    Port: 80,
    HostName: example.com
);
")]
    public sealed class EnsureIisSiteBindingOperation : RemoteEnsureOperation<IisSiteBindingConfiguration>
    {
        protected override Task<PersistedConfiguration> RemoteCollectAsync(IRemoteOperationCollectionContext context)
        {
            this.LogDebug($"Looking for site \"{this.Template.SiteName}\"...");
            using (var manager = new ServerManager())
            {
                var site = manager.Sites[this.Template.SiteName];
                if (site == null)
                {
                    this.LogInformation($"Site \"{this.Template.SiteName}\" does not exist.");
                    return Task.FromResult<PersistedConfiguration>(this.GetMissing());
                }

                var binding = this.Template.FindMatch(site.Bindings);
                if (binding == null)
                {
                    this.LogInformation($"Binding {this.Template.ConfigurationKey} does not exist.");
                    return Task.FromResult<PersistedConfiguration>(this.GetMissing());
                }

                var config = new IisSiteBindingConfiguration();
                BindingConfig.SetFromBinding(binding, config, this.Template.SiteName);
                return Task.FromResult<PersistedConfiguration>(config);
            }
        }

        protected override Task RemoteConfigureAsync(IRemoteOperationExecutionContext context)
        {
            this.LogDebug($"Looking for site \"{this.Template.SiteName}\"...");
            using (var manager = new ServerManager())
            {
                var site = manager.Sites[this.Template.SiteName];
                if (site == null)
                {
                    this.Log(
                        this.Template.Exists ? MessageLevel.Error : MessageLevel.Information,
                        $"Site \"{this.Template.SiteName}\" does not exist."
                    );

                    return Complete();
                }

                BindingConfig.Configure(this.Template, site, this);

                if (!context.Simulation)
                {
                    this.LogInformation("Committing changes...");
                    manager.CommitChanges();
                }

                return Complete();
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            string protocol = config[nameof(IisSiteBindingConfiguration.Protocol)];
            string address = config[nameof(IisSiteBindingConfiguration.Address)];
            string port = config[nameof(IisSiteBindingConfiguration.Port)];
            string hostName = config[nameof(IisSiteBindingConfiguration.HostName)];

            var desc1 = new RichDescription(
                "Ensure ",
                new Hilite(string.Join(":", protocol ?? "http", address ?? "*", port ?? "80", hostName)),
                " Binding"
            );

            var desc2 = new RichDescription(
                "on ",
                new Hilite(config[nameof(IisSiteBindingConfiguration.SiteName)]),
                " site"
            );

            if (string.Equals(config[nameof(IisSiteBindingConfiguration.Exists)], "false", StringComparison.OrdinalIgnoreCase))
                desc2.AppendContent(" does not exist");

            return new ExtendedRichDescription(desc1, desc2);
        }

        private IisSiteBindingConfiguration GetMissing()
        {
            return new IisSiteBindingConfiguration
            {
                SiteName = this.Template.SiteName,
                Protocol = this.Template.Protocol,
                Address = this.Template.Address,
                Port = this.Template.Port,
                HostName = this.Template.HostName,
                Exists = false
            };
        }
    }
}
