using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
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
    [Serializable]
    [DisplayName("IIS Site Binding")]
    public sealed class IisSiteBindingConfiguration : PersistedConfiguration, IExistential
    {
        public override string ConfigurationKey => this.SiteName + "::" + this.ConfigurationKeyWithoutSite;
        public string ConfigurationKeyWithoutSite => string.Join(":", this.Protocol, this.Address, this.Port, this.HostName);

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

        internal void EnsureBindingOnMwaSite(Site site, ILogSink log, bool createAsFirstBinding)
        {
            var desiredConfig = this;

            var binding = site.Bindings.FindMatch(desiredConfig);
            if (binding == null)
            {
                if (desiredConfig.Exists)
                {
                    log.LogInformation($"Binding {desiredConfig.ConfigurationKeyWithoutSite} does not exist; creating...");
                    addBinding();
                }
                else
                {
                    log.LogInformation($"Binding {desiredConfig.ConfigurationKeyWithoutSite} does not exist.");
                    return;
                }
            }
            else
            {
                if (desiredConfig.Exists)
                {
                    log.LogInformation($"Binding {desiredConfig.ConfigurationKeyWithoutSite} already exists.");
                    site.Bindings.Remove(binding);
                    addBinding();
                }
                else
                {
                    log.LogInformation($"Binding {desiredConfig.ConfigurationKeyWithoutSite} exists; removing...");
                    site.Bindings.Remove(binding);
                    return;
                }
            }

            void addBinding()
            {
                if (string.Equals(desiredConfig.Protocol, "https", StringComparison.OrdinalIgnoreCase))
                {
                    binding = site.Bindings.Add(desiredConfig.GetMwaBindingInformationString(), desiredConfig.GetSslCertificateThumbprint(), desiredConfig.SslCertificateStore);
                }
                else
                {
                    binding = site.Bindings.Add(desiredConfig.GetMwaBindingInformationString(), desiredConfig.Protocol);
                }
                if (createAsFirstBinding)
                {
                    site.Bindings.Remove(binding);
                    site.Bindings.AddAt(0, binding);
                }
            }

            BindingSslFlags bindingFlags;
            try
            {
                // GetAttributeValue throws a COMException if sslFlags is unavailable in some IIS versions
                bindingFlags = (BindingSslFlags)Convert.ToInt32(binding.GetAttributeValue("sslFlags") ?? 0);

                var updatedFlags = bindingFlags & ~BindingSslFlags.ServerNameIndication;
                if (desiredConfig.RequireServerNameIndication)
                    updatedFlags |= BindingSslFlags.ServerNameIndication;

                if (bindingFlags != updatedFlags)
                {
                    log.LogInformation($"Updating SSL flags to {updatedFlags}...");
                    binding.SetAttributeValue("sslFlags", (int)updatedFlags);
                }
            }
            catch (Exception ex)
            {
                if (desiredConfig.RequireServerNameIndication)
                {
                    log.LogError("Unable to set RequireSNI flag. This version of IIS may not support this feature: " + ex.Message);
                    return;
                }
            }

            log.LogInformation($"Binding \"{desiredConfig.ConfigurationKeyWithoutSite}\" {(desiredConfig.Exists ? "configured" : "removed")}.");
        }

        internal static IisSiteBindingConfiguration FromRuntimeValueMap(IDictionary<string, RuntimeValue> map)
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

            return config;
        }
        internal static IisSiteBindingConfiguration FromMwaBinding(ILogSink logger, Binding binding, string siteName, IisSiteBindingConfiguration template = null)
        {
            var config = new IisSiteBindingConfiguration
            {
                SiteName = siteName
            };
            
            var sslFlags = binding.GetSslFlagsSafe();
            var info = binding.ParseBindingInformation();

            if (template?.Address != null)
                config.Address = info.ipAddress;
            if (template?.HostName != null)
                config.HostName = info.hostName;
            if (template?.Port != null)
                config.Port = AH.ParseInt(info.port) ?? config.Port;
            if (template?.Protocol != null)
                config.Protocol = binding.Protocol;
            if (template?.RequireServerNameIndication != null)
                config.RequireServerNameIndication = sslFlags.HasFlag(BindingSslFlags.ServerNameIndication);
            if (template?.SslCertificateHash != null)
                config.SslCertificateHash = binding.FormatCertificateHash();
            if (template?.SslCertificateName != null)
                config.SslCertificateName = binding.GetCertificateName();
            if (template?.SslCertificateStore != null)
                config.SslCertificateStore = binding.CertificateStoreName;
            if (template?.SslStoreLocation != null)
                config.SslStoreLocation = sslFlags.HasFlag(BindingSslFlags.UseCentralizedStore) ? StoreLocation.LocalMachine : StoreLocation.CurrentUser;

            return config;
        }
        internal byte[] GetSslCertificateThumbprint()
        {
            if (string.IsNullOrWhiteSpace(this.SslCertificateStore))
                return null;

            if (!string.IsNullOrEmpty(this.SslCertificateHash))
                return IisSiteBindingConfiguration.ParseHash(this.SslCertificateHash);

            if (!string.IsNullOrWhiteSpace(this.SslCertificateName))
            {
                // lookup cert hash based on its friendly name
                var store = new X509Store(this.SslCertificateStore);
                try
                {
                    store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadOnly);
                    var cert = store.Certificates.Find(X509FindType.FindBySubjectName, this.SslCertificateName, true);
                    if (cert.Count == 0)
                    {
                        throw new InvalidOperationException($"Certificate \"{this.SslCertificateName}\" not found.");
                    }
                    else if (cert.Count == 1)
                    {
                        return IisSiteBindingConfiguration.ParseHash(cert[0].Thumbprint);
                    }
                    else
                    {
                        var exactMatch = cert
                            .OfType<X509Certificate2>()
                            .FirstOrDefault(c => string.Equals(c.FriendlyName, this.SslCertificateName, StringComparison.OrdinalIgnoreCase));

                        if (exactMatch != null)
                            return IisSiteBindingConfiguration.ParseHash(exactMatch.Thumbprint);
                        else
                            throw new InvalidOperationException($"Certificate \"{this.SslCertificateName}\" not found.");
                    }
                }
                finally
                {
                    store.Close();
                }
            }

            return null;
        }
        internal string GetMwaBindingInformationString() => $"{this.Address}:{this.Port}:{this.HostName}";
        private static byte[] ParseHash(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return null;

            if ((s.Length % 2) != 0)
                s = "0" + s;

            var hash = new List<byte>(20);
            for (int i = 0; i < s.Length; i += 2)
            {
                int n = parseNibble(s[i]);
                n |= parseNibble(s[i + 1]) * 16;
                hash.Add((byte)n);
            }

            return hash.ToArray();

            static int parseNibble(char c)
            {
                if (c >= '0' && c <= '9')
                    return c - '0';
                if (c >= 'A' && c <= 'F')
                    return c - 'A' + 10;
                if (c >= 'a' && c <= 'f')
                    return c - 'a' + 10;

                throw new ArgumentException($"Invalid hash value: \"{c}\" is not a valid hexadecimal digit.");
            }
        }
    }

    internal sealed class ProtocolProvider : ISuggestionProvider
    {
        public Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config) => Task.FromResult<IEnumerable<string>>(new[] { "http", "https" });
    }
}
