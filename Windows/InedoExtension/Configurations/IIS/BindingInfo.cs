using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Inedo.ExecutionEngine;
using Inedo.ExecutionEngine.Executer;
using Microsoft.Web.Administration;

namespace Inedo.Extensions.Windows.Configurations.IIS
{
    [Flags]
    public enum BindingSslFlags
    {
        None = 0,
        ServerNameIndication = 1,
        UseCentralizedStore = 2
    }

    internal sealed class BindingInfo : IEquatable<BindingInfo>
    {
        public BindingInfo(string ipAddress, string port, string hostName, string protocol, string certificateStoreName, byte[] certificateHash, BindingSslFlags sslFlags)
        {
            if (string.IsNullOrEmpty(ipAddress))
                throw new ArgumentNullException(nameof(ipAddress));
            if (string.IsNullOrEmpty(port))
                throw new ArgumentNullException(nameof(port));

            this.IpAddress = AH.CoalesceString(ipAddress.Trim(), "*");
            this.Port = port?.Trim();
            this.HostName = hostName?.Trim() ?? string.Empty;
            this.Protocol = AH.CoalesceString(protocol?.Trim(), "http");
            this.CertificateStoreName = AH.CoalesceString(certificateStoreName?.Trim(), "My");
            this.CertificateHash = certificateHash ?? new byte[0];
            this.SslFlags = sslFlags;
        }

        public string IpAddress { get; }
        public string Port { get; }
        public string HostName { get; }
        public string Protocol { get; }
        public string CertificateStoreName { get; }
        public byte[] CertificateHash { get; }
        public BindingSslFlags SslFlags { get; }

        public string BindingInformation => $"{this.IpAddress}:{this.Port}:{this.HostName}";

        public static BindingInfo FromBindingInformation(string info, string protocol) => FromBindingInformation(info, protocol, null, null);

        public static BindingInfo FromBindingInformation(string info, string protocol, string certificateStoreName, byte[] certificateHash, Binding binding = null)
        {
            if (info == null)
                return null;

            var parts = info.Split(':');

            var sslFlags = GetBindingSslFlagsSafe(binding);

            if (parts.Length == 2)
                return new BindingInfo(parts[0], parts[1], null, protocol, certificateStoreName, certificateHash, sslFlags);
            else if (parts.Length == 3)
                return new BindingInfo(parts[0], parts[1], parts[2], protocol, certificateStoreName, certificateHash, sslFlags);
            else
                return null;

            BindingSslFlags GetBindingSslFlagsSafe(Binding b)
            {
                try
                {
                    // GetAttributeValue apparently throws a COMException if sslFlags is unavailable in whatever IIS version
                    return (BindingSslFlags)Convert.ToInt32(b?.GetAttributeValue("sslFlags") ?? 0);
                }
                catch
                {
                    return BindingSslFlags.None;
                }
            }
        }

        public static BindingInfo FromMap(IReadOnlyDictionary<string, RuntimeValue> map)
        {
            if (map == null)
                return null;

            string ipAddress = map.GetValueOrDefault("IPAddress").AsString();
            string port = map.GetValueOrDefault("Port").AsString();

            if (string.IsNullOrEmpty(ipAddress) || string.IsNullOrEmpty(port))
                return null;

            string hostName = map.GetValueOrDefault("HostName").AsString();
            string certificateStoreName = map.GetValueOrDefault("CertificateStoreName").AsString();
            string protocol = map.GetValueOrDefault("Protocol").AsString();
            string certificateHash = map.GetValueOrDefault("CertificateHash").AsString();
            BindingSslFlags sslFlags = 0;
            if (map.GetValueOrDefault("ServerNameIndication").AsBoolean() ?? false)
                sslFlags |= BindingSslFlags.ServerNameIndication;
            if (map.GetValueOrDefault("UseCentralizedStore").AsBoolean() ?? false)
                sslFlags |= BindingSslFlags.UseCentralizedStore;

            return new BindingInfo(ipAddress, port, hostName, protocol, certificateStoreName, HexStringToByteArray(certificateHash), sslFlags);
        }

        public IReadOnlyDictionary<string, RuntimeValue> ToDictionary()
        {
            var dict = new Dictionary<string, RuntimeValue>();

            dict["IPAddress"] = this.IpAddress;
            dict["Port"] = this.Port;
            dict["HostName"] = this.HostName;
            dict["Protocol"] = this.Protocol;

            if (this.CertificateHash.Length > 0)
            {
                dict["CertificateHash"] = ByteArrayToHexString(this.CertificateHash);
                dict["CertificateStoreName"] = this.CertificateStoreName;
            }

            if ((this.SslFlags & BindingSslFlags.ServerNameIndication) != 0)
                dict["ServerNameIndication"] = true;
            if ((this.SslFlags & BindingSslFlags.UseCentralizedStore) != 0)
                dict["UseCentralizedStore"] = true;

            return new ReadOnlyDictionary<string, RuntimeValue>(dict);
        }

        public override string ToString()
        {
            if (this.CertificateHash.Length > 0)
                return $"{this.BindingInformation} - Cert: {ByteArrayToHexString(this.CertificateHash)} ('{this.CertificateStoreName}' store)";
            else
                return this.BindingInformation;
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(this.IpAddress ?? string.Empty)
                ^ StringComparer.OrdinalIgnoreCase.GetHashCode(this.Port ?? string.Empty);
        }

        public override bool Equals(object obj) => Equals(this, obj as BindingInfo);
        public bool Equals(BindingInfo other) => Equals(this, other);

        private static bool Equals(BindingInfo a, BindingInfo b)
        {
            if (object.ReferenceEquals(a, b))
                return true;
            if (object.ReferenceEquals(a, null) || object.ReferenceEquals(b, null))
                return false;

            return StringComparer.OrdinalIgnoreCase.Equals(a.IpAddress, b.IpAddress)
                && StringComparer.OrdinalIgnoreCase.Equals(a.Port, b.Port)
                && StringComparer.OrdinalIgnoreCase.Equals(a.Protocol, b.Protocol)
                && StringComparer.OrdinalIgnoreCase.Equals(a.CertificateStoreName, b.CertificateStoreName)
                && StringComparer.OrdinalIgnoreCase.Equals(a.HostName, b.HostName)
                && StructuralComparisons.StructuralEqualityComparer.Equals(a.CertificateHash, b.CertificateHash)
                && a.SslFlags == b.SslFlags;
        }

        private static string ByteArrayToHexString(byte[] bytes)
        {
            return string.Concat(Array.ConvertAll(bytes, x => x.ToString("X2")));
        }

        private static byte[] HexStringToByteArray(string s)
        {
            string sanitized = Regex.Replace(s ?? string.Empty, @"[^a-z0-9]", string.Empty, RegexOptions.IgnoreCase);

            if (sanitized.Length == 0 || (sanitized.Length % 2) != 0)
                return new byte[0];

            var bytes = new byte[sanitized.Length / 2];
            for (int i = 0; i < sanitized.Length; i += 2)
                bytes[i / 2] = Convert.ToByte(sanitized.Substring(i, 2), 16);

            return bytes;
        }
    }
}
