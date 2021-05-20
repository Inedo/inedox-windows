using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using Microsoft.Web.Administration;

namespace Inedo.Extensions.Windows.Configurations.IIS
{
    internal static class BindingExtensions
    {
        public static (string ipAddress, string port, string hostName) ParseBindingInformation(this Binding b)
        {
            var infoString = b.BindingInformation;
            var parts = infoString?.Split(':');
            if (parts?.Length == 2)
                return new(parts[0], parts[1], null);
            else if (parts?.Length == 3)
                return new(parts[0], parts[1], parts[2]);
            else
                return new(null, null, null);
        }
        public static BindingSslFlags GetSslFlagsSafe(this Binding b)
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
        public static string FormatCertificateHash(this Binding b)
        {
            if (b?.CertificateHash?.Length > 0)
                return string.Join(string.Empty, b.CertificateHash.Select(b => b.ToString("X2")));
            return null;
        }
        public static string GetCertificateName(this Binding b)
        {
            var location = b.GetSslFlagsSafe().HasFlag(BindingSslFlags.UseCentralizedStore) ? StoreLocation.LocalMachine : StoreLocation.CurrentUser;
            
            var store = new X509Store(b.CertificateStoreName, location);
            try
            {
                store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadOnly);
                var matches = store.Certificates.Find(X509FindType.FindByThumbprint, b.CertificateHash, true);
                if (matches.Count > 0)
                    return matches[0].FriendlyName;

                return null;
            }
            finally
            {
                store.Close();
            }

        }
        public static Binding FindMatch(this IEnumerable<Binding> bindings, IisSiteBindingConfiguration config)
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
    }
}
