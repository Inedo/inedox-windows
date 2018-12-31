using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Inedo.Diagnostics;
using Inedo.Extensibility.Configurations;
using Microsoft.Web.Administration;

namespace Inedo.Extensions.Windows.Configurations.IIS
{
    internal static class BindingConfig
    {
        public static string ConfigurationKeyWithoutSite(this ISiteBindingConfig config) => string.Join(":", config.Protocol, config.Address, config.Port, config.HostName);
        public static string BindingInformation(this ISiteBindingConfig config) => string.Join(":", config.Address, config.Port, config.HostName);
        public static byte[] ParsedHash(this ISiteBindingConfig config) => ParseHash(config.SslCertificateHash);

        public static void PopulateCertificateProperties(this ISiteBindingConfig config)
        {
            if (config.IsFullyPopulated)
                return;

            config.IsFullyPopulated = true;

            if (string.IsNullOrWhiteSpace(config.SslCertificateStore))
                return;

            if (!string.IsNullOrWhiteSpace(config.SslCertificateHash) && string.IsNullOrWhiteSpace(config.SslCertificateName))
            {
                // lookup cert name based on its hash
                if (findCert(StoreLocation.CurrentUser))
                    config.SslStoreLocation = StoreLocation.CurrentUser;
                else if (findCert(StoreLocation.LocalMachine))
                    config.SslStoreLocation = StoreLocation.LocalMachine;

                bool findCert(StoreLocation location)
                {
                    var store = new X509Store(config.SslCertificateStore, location);
                    try
                    {
                        store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadOnly);
                        var matches = store.Certificates.Find(X509FindType.FindByThumbprint, config.SslCertificateHash, true);
                        if (matches.Count > 0)
                        {
                            config.SslCertificateName = matches[0].FriendlyName;
                            return true;
                        }

                        return false;
                    }
                    finally
                    {
                        store.Close();
                    }
                }
            }
            else if (string.IsNullOrWhiteSpace(config.SslCertificateHash) && !string.IsNullOrWhiteSpace(config.SslCertificateName))
            {
                // lookup cert hash based on its friendly name
                var store = new X509Store(config.SslCertificateStore);
                try
                {
                    store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadOnly);
                    var cert = store.Certificates.Find(X509FindType.FindBySubjectName, config.SslCertificateName, true);
                    if (cert.Count == 0)
                    {
                        throw new InvalidOperationException($"Certificate \"{config.SslCertificateName}\" not found.");
                    }
                    else if (cert.Count == 1)
                    {
                        config.SslCertificateHash = cert[0].Thumbprint;
                    }
                    else
                    {
                        var exactMatch = cert
                            .OfType<X509Certificate2>()
                            .FirstOrDefault(c => string.Equals(c.FriendlyName, config.SslCertificateName, StringComparison.OrdinalIgnoreCase));

                        if (exactMatch != null)
                            config.SslCertificateHash = exactMatch.Thumbprint;
                        else
                            throw new InvalidOperationException($"Certificate \"{config.SslCertificateName}\" not found.");
                    }
                }
                finally
                {
                    store.Close();
                }
            }
        }
        public static void SetFromBinding(Binding binding, ISiteBindingConfig config, string siteName)
        {
            var flags = GetBindingSslFlagsSafe(binding);

            config.SiteName = siteName;
            config.Protocol = binding.Protocol;
            config.Address = (binding.EndPoint.Address.Equals(IPAddress.Any) || binding.EndPoint.Address.Equals(IPAddress.IPv6Any)) ? "*" : binding.EndPoint.Address.ToString();
            config.HostName = binding.Host;
            config.Port = binding.EndPoint.Port;
            config.RequireServerNameIndication = (flags & BindingSslFlags.ServerNameIndication) != 0;
            config.SslCertificateStore = binding.CertificateStoreName;
            config.SslCertificateHash = FormatHash(binding.CertificateHash);

            PopulateCertificateProperties(config);
        }
        public static ComparisonResult Compare(Func<PersistedConfiguration, ComparisonResult> baseMethod, ISiteBindingConfig config, PersistedConfiguration other)
        {
            var diffs = baseMethod(other);
            if (diffs.AreEqual)
                return diffs;

            try
            {
                PopulateCertificateProperties(config);
            }
            catch
            {
            }

            try
            {
                PopulateCertificateProperties((ISiteBindingConfig)other);
            }
            catch
            {
            }

            return baseMethod(other);
        }
        public static void Configure(ISiteBindingConfig config, Site site, ILogSink log)
        {
            var binding = config.FindMatch(site.Bindings);
            if (binding == null)
            {
                if (config.Exists)
                {
                    log.LogInformation($"Binding {config.ConfigurationKeyWithoutSite()} does not exist; creating...");
                    addBinding();
                }
                else
                {
                    log.LogInformation($"Binding {config.ConfigurationKeyWithoutSite()} does not exist.");
                    return;
                }
            }
            else
            {
                if (config.Exists)
                {
                    log.LogInformation($"Binding {config.ConfigurationKeyWithoutSite()} already exists.");
                    site.Bindings.Remove(binding);
                    addBinding();
                }
                else
                {
                    log.LogInformation($"Binding {config.ConfigurationKeyWithoutSite()} exists; removing...");
                    site.Bindings.Remove(binding);
                    return;
                }
            }

            void addBinding()
            {
                if (string.Equals(config.Protocol, "https", StringComparison.OrdinalIgnoreCase))
                {
                    config.PopulateCertificateProperties();
                    binding = site.Bindings.Add(config.BindingInformation(), config.ParsedHash(), config.SslCertificateStore);
                }
                else
                {
                    binding = site.Bindings.Add(config.BindingInformation(), config.Protocol);
                }
            }

            BindingSslFlags bindingFlags;
            try
            {
                // GetAttributeValue throws a COMException if sslFlags is unavailable in some IIS versions
                bindingFlags = (BindingSslFlags)Convert.ToInt32(binding.GetAttributeValue("sslFlags") ?? 0);

                var updatedFlags = bindingFlags & ~BindingSslFlags.ServerNameIndication;
                if (config.RequireServerNameIndication)
                    updatedFlags |= BindingSslFlags.ServerNameIndication;

                if (bindingFlags != updatedFlags)
                {
                    log.LogInformation($"Updating SSL flags to {updatedFlags}...");
                    binding.SetAttributeValue("sslFlags", (int)updatedFlags);
                }
            }
            catch (Exception ex)
            {
                if (config.RequireServerNameIndication)
                {
                    log.LogError("Unable to set RequireSNI flag. This version of IIS may not support this feature: " + ex.Message);
                    return;
                }
            }

            log.LogInformation($"Binding \"{config.ConfigurationKeyWithoutSite()}\" {(config.Exists ? "configured" : "removed")}.");
        }
        public static Binding FindMatch(this ISiteBindingConfig config, IEnumerable<Binding> bindings)
        {
            var address = (string.IsNullOrWhiteSpace(config.Address) || config.Address == "*") ? IPAddress.Any : IPAddress.Parse(config.Address);

            foreach (var b in bindings)
            {
                if (!string.Equals(config.Protocol, b.Protocol, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!address.Equals(b.EndPoint.Address ?? IPAddress.Any))
                    continue;

                if (config.Port != b.EndPoint.Port)
                    continue;

                if (!string.Equals(config.HostName ?? string.Empty, b.Host ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    continue;

                return b;
            }

            return null;
        }

        private static string FormatHash(byte[] hash)
        {
            if (hash == null || hash.Length == 0)
                return null;

            return string.Join(string.Empty, hash.Select(b => b.ToString("X2")));
        }
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

            int parseNibble(char c)
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
        private static BindingSslFlags GetBindingSslFlagsSafe(Binding b)
        {
            try
            {
                // GetAttributeValue throws a COMException if sslFlags is unavailable in some IIS versions
                return (BindingSslFlags)Convert.ToInt32(b?.GetAttributeValue("sslFlags") ?? 0);
            }
            catch
            {
                return BindingSslFlags.None;
            }
        }
    }
}
