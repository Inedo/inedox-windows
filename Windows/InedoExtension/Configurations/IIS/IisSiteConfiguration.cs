using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using Inedo.Serialization;
using Inedo.Web;
using Microsoft.Web.Administration;

namespace Inedo.Extensions.Windows.Configurations.IIS
{
    [DisplayName("IIS Site")]
    [Description("Describes an IIS Site with a single application and a single virtual directory.")]
    [DefaultProperty(nameof(Name))]
    [PersistFrom("Inedo.Otter.Extensions.Configurations.IIS.IisSiteConfiguration,OtterCoreEx")]
    [Serializable]
    public sealed class IisSiteConfiguration : IisConfigurationBase
    {
        [Required]
        [Persistent]
        [ConfigurationKey]
        [ScriptAlias("Name")]
        [Description("The unique name of the IIS site or application pool.")]
        public string Name { get; set; }

        [Persistent]
        [ScriptAlias("AppPool")]
        [DisplayName("Application pool")]
        [Description("The name of the application pool assigned to the site.")]
        public string ApplicationPoolName { get; set; }

        [Persistent]
        [ScriptAlias("Path")]
        [DisplayName("Virtual directory physical path")]
        [Description("The path to the web site files on disk.")]
        public string VirtualDirectoryPhysicalPath { get; set; }

        [Persistent]
        [Category("Binding")]
        [ScriptAlias("BindingProtocol")]
        [SuggestableValue(typeof(ProtocolProvider))]
        public string BindingProtocol { get; set; }
        [Persistent]
        [DefaultValue("*")]
        [Category("Binding")]
        [ScriptAlias("BindingAddress")]
        [DisplayName("IP address")]
        public string BindingAddress { get; set; } = "*";
        [Persistent]
        [Category("Binding")]
        [ScriptAlias("BindingHostName")]
        [DisplayName("Host name")]
        public string BindingHostName { get; set; }
        [Persistent]
        [DefaultValue(80)]
        [Category("Binding")]
        [ScriptAlias("BindingPort")]
        public int BindingPort { get; set; } = 80;
        [Persistent]
        [Category("SSL")]
        [ScriptAlias("BindingCertficiate")]
        [DisplayName("SSL certificate")]
        [PlaceholderText("friendly name, if not using \"CertificateHash\"")]
        public string BindingSslCertificateName { get; set; }
        [Persistent]
        [Category("SSL")]
        [DisplayName("Certificate store location")]
        [ScriptAlias("BindingCertificateStoreLocation")]
        public StoreLocation BindingSslStoreLocation { get; set; } = StoreLocation.CurrentUser;
        [Persistent]
        [Category("SSL")]
        [ScriptAlias("BindingCertificateHash")]
        [DisplayName("SSL certificate hash")]
        [Description("When specified, this value will be used to identify the SSL certificate by its thumbprint, and the \"Certificate\" and \"CertificateStoreLocation\" values will be ignored.")]
        public string BindingSslCertificateHash { get; set; }
        [Persistent]
        [Category("SSL")]
        [ScriptAlias("BindingRequireSNI")]
        [DisplayName("Require SNI")]
        public bool BindingRequireServerNameIndication { get; set; }
        [Persistent]
        [Category("SSL")]
        [DefaultValue("My")]
        [IgnoreConfigurationDrift]
        [ScriptAlias("BindingCertificateStore")]
        [DisplayName("SSL certificate store")]
        public string BindingSslCertificateStore { get; set; } = "My";

        [EditorBrowsable(EditorBrowsableState.Never)]
        [ScriptAlias("Binding", Obsolete = true)]
        [IgnoreConfigurationDrift]
        public string LegacyBindingInformation { get; set; }
        [Persistent]
        [Category("Advanced")]
        [ScriptAlias("Bindings", Obsolete = true)]
        [DisplayName(MultipleBindingsDisplayName)]
        [FieldEditMode(FieldEditMode.Multiline)]
        [Description(@"This setting is no longer recommended; instead, use a separate operation to set multiple bindings. To enter multiple bindings, use a list of maps, e.g.:<br/><pre style=""white-space: pre-wrap;"">@(%(IPAddress: 192.0.2.100, Port: 80, HostName: example.com, Protocol: http), %(IPAddress: 192.0.2.101, Port: 443, HostName: secure.example.com, Protocol: https, CertificateStoreName: WebHosting, CertificateHash: 51599BF2909EA984793481F0DF946C57E4FD5DEA, ServerNameIndication: true, UseCentralizedStore: false))</pre>")]
        public IEnumerable<IDictionary<string, RuntimeValue>> MultipleBindings { get; set; }
        private const string MultipleBindingsDisplayName = "Multiple bindings (Legacy)";


        internal static IisSiteConfiguration FromMwaSite(ILogSink logger, Site mwaSite, IisSiteConfiguration template = null)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (mwaSite == null)
                throw new ArgumentNullException(nameof(mwaSite));

            var config = new IisSiteConfiguration
            {
                Name = mwaSite.Name
            };

            var app = mwaSite.Applications.FirstOrDefault();
            if (app == null)
            {
                logger.LogWarning("Site does not have an application configured.");
            }
            else
            {
                var vdir = app.VirtualDirectories.FirstOrDefault();
                if (vdir == null)
                {
                    logger.LogWarning("Site does not have an VirtualDirectory configured.");
                }
                else
                {
                    if (template?.ApplicationPoolName != null)
                        config.ApplicationPoolName = app.ApplicationPoolName;

                    if (template?.VirtualDirectoryPhysicalPath != null)
                        config.VirtualDirectoryPhysicalPath = vdir.PhysicalPath;
                }
            }

            if (mwaSite.Bindings.Count == 0)
            {
                logger.LogWarning("Site does not have a Binding configured.");
            }
            else
            {
                if (template?.MultipleBindings != null)
                {
                    var configMultipleBindings = new List<IDictionary<string, RuntimeValue>>();
                    var templateMultipleBindings = template.MultipleBindings.ToList();

                    for (int i = 0; i < templateMultipleBindings.Count && i < mwaSite.Bindings.Count; i++)
                    {
                        var configBinding = new Dictionary<string, RuntimeValue>();
                        var templateBinding = templateMultipleBindings[i];
                        var mwaBinding = mwaSite.Bindings[i];

                        var (ipAddress, port, hostName) = mwaBinding.ParseBindingInformation();

                        if (templateBinding.ContainsKey("IPAddress"))
                            configBinding["IPAddress"] = ipAddress;
                        if (templateBinding.ContainsKey("Port"))
                            configBinding["Port"] = port;
                        if (templateBinding.ContainsKey("HostName"))
                            configBinding["HostName"] = hostName;

                        if (templateBinding.ContainsKey("CertificateStoreName"))
                            configBinding["CertificateStoreName"] = mwaBinding.CertificateStoreName;
                        if (templateBinding.ContainsKey("Protocol"))
                            configBinding["Protocol"] = mwaBinding.Protocol;

                        if (templateBinding.ContainsKey("CertificateHash"))
                            configBinding["CertificateHash"] = mwaBinding.FormatCertificateHash();
                        if (templateBinding.ContainsKey("ServerNameIndication"))
                            configBinding["ServerNameIndication"] = mwaBinding.GetSslFlagsSafe().HasFlag(BindingSslFlags.ServerNameIndication);
                        if (templateBinding.ContainsKey("UseCentralizedStore"))
                            configBinding["UseCentralizedStore"] = mwaBinding.GetSslFlagsSafe().HasFlag(BindingSslFlags.UseCentralizedStore);

                        
                        configMultipleBindings.Add(new RuntimeValue(configBinding).AsDictionary());
                    }
                    config.MultipleBindings = configMultipleBindings;
                }
                else
                {
                    var firstSiteConfig = IisSiteBindingConfiguration.FromMwaBinding(logger, mwaSite.Bindings.First(), mwaSite.Name, template?.GetSingleBindingConfiguration());

                    if (template?.BindingAddress != null)
                        config.BindingAddress = firstSiteConfig.Address;
                    if (template?.BindingHostName != null)
                        config.BindingHostName = firstSiteConfig.HostName;
                    if (template?.BindingPort != null)
                        config.BindingPort = firstSiteConfig.Port;
                    if (template?.BindingProtocol != null)
                        config.BindingProtocol = firstSiteConfig.Protocol;
                    if (template?.BindingRequireServerNameIndication != null)
                        config.BindingRequireServerNameIndication = firstSiteConfig.RequireServerNameIndication;
                    if (template?.BindingSslCertificateHash != null)
                        config.BindingSslCertificateHash = firstSiteConfig.SslCertificateHash;
                    if (template?.BindingSslCertificateName != null)
                        config.BindingSslCertificateName = firstSiteConfig.SslCertificateName;
                    if (template?.BindingSslCertificateStore != null)
                        config.BindingSslCertificateStore = firstSiteConfig.SslCertificateStore;
                    if (template?.BindingSslStoreLocation != null)
                        config.BindingSslStoreLocation = firstSiteConfig.SslStoreLocation;
                }
            }

            return config;
        }

        public static void SetMwaSite(ILogSink logger, IisSiteConfiguration config, Site site)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (site == null)
                throw new ArgumentNullException(nameof(site));

            Application app;
            if (site.Applications.Count > 1)
            {
                logger.LogDebug(@"Site has more than one Application defined; using application with path=""/"".");
                app = site.Applications.FirstOrDefault(a => a.Path == "/");
            }
            else
            {                
                app = site.Applications.FirstOrDefault();
            }

            if (app == null)
            {
                logger.LogDebug("Site does not have an Application; creating Application...");
                app = site.Applications.Add("/", config.VirtualDirectoryPhysicalPath);
            }

            var vdir = app.VirtualDirectories.FirstOrDefault();
            if (vdir == null)
            {
                logger.LogDebug("Application does not have a Virtual Directory; creating Virtual Directory...");
                vdir = app.VirtualDirectories.Add("/", config.VirtualDirectoryPhysicalPath);
            }

            if (config.ApplicationPoolName != null)
                app.ApplicationPoolName = config.ApplicationPoolName;

            if (config.VirtualDirectoryPhysicalPath != null)
                vdir.PhysicalPath = config.VirtualDirectoryPhysicalPath;

            if (config.MultipleBindings != null)
            {
                logger.LogDebug("Clearing bindings...");
                site.Bindings.Clear();

                logger.LogDebug("Setting bindings...");
                foreach (var bindingConfig in config.GetMultipleBindingConfigurations())
                {
                    bindingConfig.EnsureBindingOnMwaSite(site, logger, false);
                }
            }
            else
            {
                var singleBinding = config.GetSingleBindingConfiguration();
                if (singleBinding != null)
                    singleBinding.EnsureBindingOnMwaSite(site, logger, true);
            }
        }

        public override async Task<ComparisonResult> CompareAsync(PersistedConfiguration other, IOperationCollectionContext context)
        {
            var baseResults = await base.CompareAsync(other, context);
            
            if (this.MultipleBindings == null)
                return baseResults;

            var differences = baseResults.Differences.Where(d => d.Name != nameof(this.MultipleBindings)).ToList();

            var otherBindings = ((IisSiteConfiguration)other).MultipleBindings;
            if (otherBindings == null)
                otherBindings = Enumerable.Empty<IDictionary<string, RuntimeValue>>();

            var thisBindingInfos = this.MultipleBindings.Select(b => b.ToString()).ToHashSet();
            var otherBindingInfos = otherBindings.Select(b => b.ToString()).ToHashSet();

            if (thisBindingInfos.SetEquals(otherBindingInfos))
                return new ComparisonResult(differences);

            var diff = new Difference(nameof(this.MultipleBindings), string.Join("; ", thisBindingInfos), string.Join("; ", otherBindingInfos));
            differences.Add(diff);
            return new ComparisonResult(differences);
        }

        public IEnumerable<IisSiteBindingConfiguration> GetMultipleBindingConfigurations()
        {
            if (this.MultipleBindings == null)
                yield break;

            foreach (var map in this.MultipleBindings)
            {
                var config = new IisSiteBindingConfiguration();

                if (map.ContainsKey("IPAddress"))
                    config.Address = map["IPAddress"].AsString();
                if (map.ContainsKey("Port"))
                    config.Port = map["Port"].AsInt32() ?? config.Port;
                if (map.ContainsKey("HostName"))
                    config.HostName = map["HostName"].AsString();
                if (map.ContainsKey("CertificateStoreName"))
                    config.SslCertificateStore = map["CertificateStoreName"].AsString();
                if (map.ContainsKey("Protocol"))
                    config.Protocol = map["Protocol"].AsString();
                if (map.ContainsKey("CertificateHash"))
                    config.SslCertificateHash = map["CertificateHash"].AsString();
                if (map.ContainsKey("ServerNameIndication"))
                    config.RequireServerNameIndication = map["ServerNameIndication"].AsBoolean() ?? config.RequireServerNameIndication;
                if (map.ContainsKey("UseCentralizedStore"))
                    config.SslStoreLocation = map["UseCentralizedStore"].AsBoolean() == true ? StoreLocation.LocalMachine : StoreLocation.CurrentUser;

                yield return config;
            }
        }
        public IisSiteBindingConfiguration GetSingleBindingConfiguration()
        {
            if (string.IsNullOrEmpty(this.BindingProtocol))
                return null;

            return new IisSiteBindingConfiguration
            {
                Address = this.BindingAddress,
                HostName = this.BindingHostName,
                Port = this.BindingPort,
                Protocol = this.BindingProtocol,
                RequireServerNameIndication = this.BindingRequireServerNameIndication,
                SslCertificateHash = this.BindingSslCertificateHash,
                SslCertificateName = this.BindingSslCertificateName,
                SslCertificateStore = this.BindingSslCertificateStore,
                SslStoreLocation = this.BindingSslStoreLocation
            };
        }
    }
}
