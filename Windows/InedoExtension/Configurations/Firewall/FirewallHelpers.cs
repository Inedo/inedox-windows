using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WindowsFirewallHelper;

namespace Inedo.Extensions.Windows.Configurations.Firewall
{
    internal static class FirewallHelpers
    {
        private static Lazy<IDictionary<string, FirewallProtocol>> protocols = new Lazy<IDictionary<string, FirewallProtocol>>(() => typeof(FirewallProtocol).GetFields(BindingFlags.Static | BindingFlags.Public).ToDictionary(f => f.Name, f => (FirewallProtocol)f.GetValue(null)));

        private static IDictionary<string, FirewallProtocol> Protocols => protocols.Value;

        public static string GetProtocalString(this FirewallProtocol protocol)
        {
            var p = Protocols.Where(p => p.Value == protocol).Select(p => p.Key).SingleOrDefault();
            return p ?? protocol?.ToString();
        }

        public static FirewallProtocol GetProtocalValue(this string protocol)
        {
            return Protocols[protocol];
        }

        public static T ParseEnumValue<T>(this string value, T defaultValue) where T : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;
            if (!Enum.TryParse<T>(value, out T val))
                val = defaultValue;
            return val;
        }

        public static FirewallProfiles GetFirewallProfiles(this string profiles)
        {
            var profileEnums = profiles.Split(',').Select(p => (FirewallProfiles)Enum.Parse(typeof(FirewallProfiles), p.Trim().ToLower(), true));
            return profileEnums.Aggregate((current, item) => current | item);
        }
    }
}
