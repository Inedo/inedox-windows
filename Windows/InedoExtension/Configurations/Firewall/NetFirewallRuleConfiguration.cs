﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using Inedo.Serialization;
using WindowsFirewallHelper;

namespace Inedo.Extensions.Windows.Configurations.Firewall
{
    [Serializable]
    [DisplayName("Firewall Rule")]

    public sealed class NetFirewallRuleConfiguration : PersistedConfiguration, IExistential
    {
        [Persistent]
        [Required]
        [ConfigurationKey]
        [ScriptAlias("Name")]
        [DisplayName("Name")]
        public string Name { get; set; }

        [Persistent]
        [Required]
        [ScriptAlias("Profiles")]
        [DisplayName("Profiles")]
        [Description("Specify a comma separated list of profiles: \"Public\", \"Private\", and/or \"Domain\". (ex: \"Public, Private\")")]
        public string Profiles { get; set; }

        [Persistent]
        [Required]
        [ScriptAlias("Port")]
        [DisplayName("Port or Port Range")]
        [Description("Specify the port(s) affected by the firewall rule.  Ports can be a comma separated list or a port range specified as \"start-end\" ex: 80-81,443")]
        public string Port { get; set; }

        [Persistent]
        [Required]
        [ScriptAlias("Protocol")]
        [DisplayName("Protocol")]
        [Description("Specify if the protocol is \"UDP\" or \"TCP\"")]
        public string Protocol { get; set; }

        [Persistent]
        [Required]
        [DefaultValue("Inbound")]
        [ScriptAlias("Direction")]
        [DisplayName("Direction")]
        [Description("Specify if the connection is Inbound or Outbound. (Default = Inbound)")]
        public string Direction { get; set; } = "Inbound";

        [Persistent]
        [Required]
        [DefaultValue("Allow")]
        [ScriptAlias("Action")]
        [DisplayName("Action")]
        [Description("Select if you want to Allow or Block a connection. (Default = Allow)")]
        public string Action { get; set; } = "Allow";

        [Persistent]
        [DefaultValue(true)]
        [ScriptAlias("Exists")]
        public bool Exists { get; set; } = true;

        public override string ConfigurationKey => this.Name.Replace(" ", string.Empty);

        public static NetFirewallRuleConfiguration GetRule(string ruleName, string direction = "Inbound")
        {
            if (string.IsNullOrEmpty(ruleName))
                throw new ArgumentNullException(nameof(ruleName));

            if (!Enum.TryParse(direction, out FirewallDirection firewallDirection))
            {
                firewallDirection = FirewallDirection.Inbound;
            }

            var firewall = FirewallManager.Instance;
            return firewall.Rules.Where(r => r.Name.Equals(ruleName, StringComparison.OrdinalIgnoreCase) && r.Direction == firewallDirection).Select(r => new NetFirewallRuleConfiguration
            {
                Name = r.Name,
                Profiles = r.Profiles.ToString(),
                Port = r.Direction == FirewallDirection.Inbound ? string.Join(",", r.LocalPorts) : string.Join(",", r.RemotePorts),
                Protocol = r.Protocol.GetProtocalString(),
                Direction = r.Direction.ToString(),
                Action = r.Action.ToString(),
                Exists = true

            }).FirstOrDefault() ?? new NetFirewallRuleConfiguration { Name = ruleName, Exists = false};

        }

        public override Task<ComparisonResult> CompareAsync(PersistedConfiguration other, IOperationCollectionContext context)
        {
            if (other is not NetFirewallRuleConfiguration rule)
                throw new ArgumentException("Cannot compare configurations of different types.");

            var differences = new List<Difference>();
            if (!this.Exists || !rule.Exists)
            {
                if (this.Exists || rule.Exists)
                {
                    differences.Add(new Difference(nameof(Exists), this.Exists, rule.Exists));
                }

                return Task.FromResult(new ComparisonResult(differences));
            }
            if(!CompareProfiles(rule.Profiles))
            {
                differences.Add(new Difference(nameof(Profiles), this.Profiles, rule.Profiles));
            }

            if (this.ParsedPort().SequenceEqual(rule.ParsedPort()))
            {
                differences.Add(new Difference(nameof(Port), this.Port, rule.Port));
            }

            if (!this.Protocol.Equals(rule.Protocol, StringComparison.OrdinalIgnoreCase))
            {
                differences.Add(new Difference(nameof(Protocol), this.Protocol, rule.Protocol));
            }

            if (!this.Direction.Equals(rule.Direction, StringComparison.OrdinalIgnoreCase))
            {
                differences.Add(new Difference(nameof(Direction), this.Direction, rule.Direction));
            }

            if (!this.Action.Equals(rule.Action, StringComparison.OrdinalIgnoreCase))
            {
                differences.Add(new Difference(nameof(Action), this.Action, rule.Action));
            }

            return Task.FromResult(new ComparisonResult(differences));
        }

        private bool CompareProfiles(string other)
        {
            if (this.Profiles == null || other == null)
                return this.Profiles == other;
            var thisProfiles = this.Profiles.Split(',').Select(p => p.Trim().ToLower()).OrderBy(p => p);
            var otherProfiles = other.Split(',').Select(p => p.Trim().ToLower()).OrderBy(p => p);
            return thisProfiles.SequenceEqual(otherProfiles);
        }

        public ushort[] ParsedPort()
        {
            var parsedPorts = new List<ushort>();
            var ports = this.Port.Split(',');
            foreach(var port in ports)
            {
                if (port.Contains("-"))
                {
                    var range = port.Split('-');
                    if (range.Length != 2)
                        throw new FormatException($"Invalid port format for Local Ports: \"{this.Port}\"");

                    var begin = int.Parse(range[0].Trim());
                    var end = int.Parse(range[1].Trim());
                    if (begin == end)
                    {
                        parsedPorts.Add(Convert.ToUInt16(begin));
                        continue;
                    }
                    if (end < begin)
                    {
                        throw new FormatException($"Invalid port format for Local Ports: \"{this.Port}\"");
                    }
                    parsedPorts.AddRange(Enumerable.Range(begin, end).Select(p => Convert.ToUInt16(p)));

                }
                else
                {
                    parsedPorts.Add(ushort.Parse(port.Trim()));
                }
            }
            return parsedPorts.ToArray();
        }

        public void DeleteRule()
        {
            if (!Enum.TryParse(this.Direction, out FirewallDirection direction))
                direction = FirewallDirection.Inbound;

            var firewall = FirewallManager.Instance;
            foreach(var rule in firewall.Rules.Where(r => r.Name.Equals(this.Name, StringComparison.OrdinalIgnoreCase) && r.Direction == direction).ToList())
            {
                firewall.Rules.Remove(firewall.Rules.Where(r => r.Name.Equals(this.Name, StringComparison.OrdinalIgnoreCase) && r.Direction == direction).FirstOrDefault());
            }
        }

        public void CreateRule()
        {
            var firewall = FirewallManager.Instance;
            var rule = firewall.CreatePortRule(this.Profiles.GetFirewallProfiles(), this.Name, this.Action.ParseEnumValue(FirewallAction.Allow), 80, this.Protocol.GetProtocalValue());
            rule.Direction = this.Direction.ParseEnumValue(FirewallDirection.Inbound);
            if (rule.Direction == FirewallDirection.Inbound)
                rule.LocalPorts = this.ParsedPort();
            else
                rule.RemotePorts = this.ParsedPort();

            firewall.Rules.Add(rule);
        }
    }
}
