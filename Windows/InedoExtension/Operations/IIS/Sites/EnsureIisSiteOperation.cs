using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Windows.Configurations.IIS;
using Microsoft.Web.Administration;

namespace Inedo.Extensions.Windows.Operations.IIS.Sites
{
    [Serializable]
    [DisplayName("Ensure Site")]
    [Description("Ensures the existence of a site on a server.")]
    [ScriptAlias("Ensure-Site")]
    [Tag(Tags.IIS)]
    [ScriptNamespace(Namespaces.IIS)]
    [Tag(Tags.Sites)]
    [SeeAlso(typeof(AppPools.EnsureIisAppPoolOperation))]
    [Note("When creating a site, you must specify binding information.")]
    [Example(@"
# ensures that the FooBar web site is present on the web server, and binds the site to the single IP address 192.0.2.100  and hostname ""foorbar.corp""
IIS::Ensure-Site(
    Name: FooBar,
    AppPool: FooBarAppPool,
    Path: E:\Websites\FooBar,
    BindingProtocol: http,
    BindingAddress: 192.0.2.100,
    BindingHostName: foobar.corp
);

# ensures that the Default Web Site is removed from the web server
IIS::Ensure-Site(
    Name: Default Web Site,
    Exists: false
);
")]
    public sealed class EnsureIisSiteOperation : RemoteEnsureOperation<IisSiteConfiguration>
    {
        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            var shortDesc = new RichDescription("Ensure IIS Site: ", new Hilite(config[nameof(IisSiteConfiguration.Name)]));

            string appPool = config[nameof(IisSiteConfiguration.ApplicationPoolName)];
            string vdir = config[nameof(IisSiteConfiguration.VirtualDirectoryPhysicalPath)];
            bool explicitDoesNotExist = string.Equals(config[nameof(IisSiteConfiguration.Exists)], "false", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(appPool) || string.IsNullOrEmpty(vdir) || explicitDoesNotExist)
                return new ExtendedRichDescription(shortDesc, new RichDescription("does not exist"));
            else
                return new ExtendedRichDescription(shortDesc, new RichDescription("application pool ", new Hilite(appPool), "; virtual directory path: ", new Hilite(vdir)));
        }

        private void HandleLegacyBindingInformation()
        {
            if (!string.IsNullOrEmpty(this.Template?.LegacyBindingInformation))
            {
                this.LogWarning($"This operation uses a legacy binding string ({this.Template?.LegacyBindingInformation}), which is no longer supported.");

                var parts = this.Template.LegacyBindingInformation.Split(':');
                if (parts.Length >= 2)
                {
                    this.Template.BindingAddress = parts[0];
                    this.Template.BindingPort = AH.ParseInt(parts[1]) ?? this.Template.BindingPort;
                }
                if (parts.Length >= 3)
                {
                    this.Template.BindingHostName = parts[2];
                }
                this.Template.LegacyBindingInformation = null;
            }
        }

        protected override Task BeforeRemoteCollectAsync(IOperationCollectionContext context)
        {
            this.HandleLegacyBindingInformation();
            return base.BeforeRemoteCollectAsync(context);
        }
        protected override Task BeforeRemoteConfigureAsync(IOperationExecutionContext context)
        {
            this.HandleLegacyBindingInformation();
            return base.BeforeRemoteConfigureAsync(context);
        }

        protected override Task<PersistedConfiguration> RemoteCollectAsync(IRemoteOperationCollectionContext context)
        {
            this.LogDebug($"Looking for Site \"{this.Template.Name}\"...");
            using (var manager = new ServerManager())
            {
                var site = manager.Sites[this.Template.Name];
                if (site == null)
                {
                    this.LogInformation($"Site \"{this.Template.Name}\" does not exist.");
                    return Task.FromResult<PersistedConfiguration>(new IisSiteConfiguration { Name = this.Template.Name, Exists = false });
                }

                return Task.FromResult<PersistedConfiguration>(IisSiteConfiguration.FromMwaSite(this, site, this.Template));
            }
        }

        protected override Task RemoteConfigureAsync(IRemoteOperationExecutionContext context)
        {
            this.LogDebug($"Looking for Site \"{this.Template.Name}\"...");
            using (var manager = new ServerManager())
            {
                var site = manager.Sites[this.Template.Name];
                if (this.Template.Exists)
                {
                    if (!string.IsNullOrWhiteSpace(this.Template.ApplicationPoolName))
                    {
                        if (manager.ApplicationPools[this.Template.ApplicationPoolName] == null)
                        {
                            this.Log(
                                context.Simulation ? MessageLevel.Warning : MessageLevel.Error,
                                $"The specified application pool ({this.Template.ApplicationPoolName}) does not exist."
                            );

                            if (!context.Simulation)
                                return Complete();
                        }
                    }

                    if (site == null)
                    {
                        this.LogDebug("Does not exist. Creating...");
                        if (!context.Simulation)
                        {
                            string mwaBindingInfo, bindingProtocol;

                            var simpleBinding = this.Template.GetSingleBindingConfiguration();
                            if (simpleBinding != null)
                            {
                                mwaBindingInfo = simpleBinding.GetMwaBindingInformationString();
                                bindingProtocol = simpleBinding.Protocol;
                            }
                            else
                            {
                                var firstMultipleBinding = this.Template.MultipleBindings?.FirstOrDefault();
                                if (firstMultipleBinding != null)
                                {
                                    var binding = IisSiteBindingConfiguration.FromRuntimeValueMap(firstMultipleBinding);
                                    if (binding == null)
                                        throw new ExecutionFailureException("Binding info from MultipleBindings could not be parsed. At a minimum, 'IPAddress' and 'Port' must be specified.");

                                    mwaBindingInfo = binding.GetMwaBindingInformationString();
                                    bindingProtocol = binding.Protocol;
                                }
                                else
                                    throw new ExecutionFailureException("You must specify binding information when creating a site.");
                            }

                            this.LogDebug("Does not exist. Creating...");
                            site = manager.Sites.Add(this.Template.Name, bindingProtocol, mwaBindingInfo, this.Template.VirtualDirectoryPhysicalPath);
                            manager.CommitChanges();
                        }

                        this.LogInformation($"Site \"{this.Template.Name}\" added.");
                        this.LogDebug("Reloading configuration...");
                        if (!context.Simulation)
                            site = manager.Sites[this.Template.Name];
                    }

                    this.LogDebug("Applying configuration...");
                    if (!context.Simulation)
                        IisSiteConfiguration.SetMwaSite(this, this.Template, site);
                }
                else
                {
                    if (site == null)
                    {
                        this.LogWarning("Site does not exist.");
                        return Complete();
                    }

                    if (!context.Simulation)
                        manager.Sites.Remove(site);
                }

                this.LogDebug("Committing configuration...");
                if (!context.Simulation)
                    manager.CommitChanges();

                this.LogInformation($"Site \"{this.Template.Name}\" {(this.Template.Exists ? "configured" : "removed")}.");
            }

            return Complete();
        }
    }
}
