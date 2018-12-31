using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.Windows.Configurations.IIS
{
    [Serializable]
    [DisplayName("IIS Site Binding")]
    public sealed class IisSiteBindingConfiguration : PersistedConfiguration, IExistential, ISiteBindingConfig
    {
        public override string ConfigurationKey => this.SiteName + "::" + this.ConfigurationKeyWithoutSite();

        [Required]
        [Persistent]
        [ScriptAlias("Site")]
        [DisplayName("IIS site")]
        public string SiteName { get; set; }
        [Persistent]
        [DefaultValue("http")]
        [ScriptAlias("Protocol")]
        [SuggestableValue(typeof(ProtocolProvider))]
        public string Protocol { get; set; } = "http";
        [Persistent]
        [DefaultValue("*")]
        [ScriptAlias("Address")]
        [DisplayName("IP address")]
        public string Address { get; set; } = "*";
        [Persistent]
        [ScriptAlias("HostName")]
        [DisplayName("Host name")]
        public string HostName { get; set; }
        [Persistent]
        [DefaultValue(80)]
        [ScriptAlias("Port")]
        public int Port { get; set; } = 80;
        [Persistent]
        [Category("SSL")]
        [ScriptAlias("Certficiate")]
        [DisplayName("SSL certificate")]
        [PlaceholderText("friendly name, if not using \"CertificateHash\"")]
        public string SslCertificateName { get; set; }
        [Persistent]
        [Category("SSL")]
        [DisplayName("Certificate store location")]
        [ScriptAlias("CertificateStoreLocation")]
        public StoreLocation SslStoreLocation { get; set; } = StoreLocation.CurrentUser;
        [Persistent]
        [Category("SSL")]
        [ScriptAlias("CertificateHash")]
        [DisplayName("SSL certificate hash")]
        [Description("When specified, this value will be used to identify the SSL certificate by its thumbprint, and the \"Certificate\" and \"CertificateStoreLocation\" values will be ignored.")]
        public string SslCertificateHash { get; set; }
        [Persistent]
        [Category("SSL")]
        [ScriptAlias("RequireSNI")]
        [DisplayName("Require SNI")]
        public bool RequireServerNameIndication { get; set; }
        [Persistent]
        [Category("SSL")]
        [DefaultValue("My")]
        [IgnoreConfigurationDrift]
        [ScriptAlias("CertificateStore")]
        [DisplayName("SSL certificate store")]
        public string SslCertificateStore { get; set; } = "My";
        [Persistent]
        [DefaultValue(true)]
        [ScriptAlias("Exists")]
        public bool Exists { get; set; } = true;

        bool ISiteBindingConfig.IsFullyPopulated { get; set; }

        public override ComparisonResult Compare(PersistedConfiguration other) => BindingConfig.Compare(base.Compare, this, other);
    }

    internal sealed class ProtocolProvider : ISuggestionProvider
    {
        public Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config) => Task.FromResult<IEnumerable<string>>(new[] { "http", "https" });
    }
}
