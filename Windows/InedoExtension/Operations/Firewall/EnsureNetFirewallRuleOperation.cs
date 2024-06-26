﻿using System.ComponentModel;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Windows.Configurations.Firewall;

namespace Inedo.Extensions.Windows.Operations.Firewall
{
    [Serializable]
    [DisplayName("Ensure Firewall Rule")]
    [Description("Ensures the existence of a firewall rule on a Windows server.")]
    [ScriptAlias("Ensure-NetFirewallRule")]
    [Tag(Tags.Firewall)]
    [ScriptNamespace(Namespaces.Firewall)]
    [Example("""
        # ensures that TCP ports 80 and 443 are allowed on "Domain" and Private profiles in Window's Firewall
        Firewall::Ensure-NetFirewallRule(
            Name: OtterHttpTCP80443,
            Profiles: "Domain, Private",
            Port: "80,443",
            Protocol: TCP,
            Inbound: true,
            Allow: true
        );

        # ensures that UDP ports 5000 through 5004 and 5008 are allowed on the "Domain" profile Window's Firewall
        Firewall::Ensure-NetFirewallRule(
            Name: OtterHttpUdpTest,
            Profiles: "Domain",
            Port: "5000-5004,5008",
            Protocol: UDP,
            Inbound: true,
            Allow: true
        );

        # ensures that the "OtterHttpTCP80443" Window's Firewall rule is removed
        IIS::Ensure-Site(
            Name: OtterHttpTCP80443,
            Exists: false
        );
        """)]
    public sealed class EnsureNetFirewallRuleOperation : RemoteEnsureOperation<NetFirewallRuleConfiguration>
    {
        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            var richDesc = new ExtendedRichDescription(
                new RichDescription(
                    "Ensure ",
                    new Hilite(config[nameof(NetFirewallRuleConfiguration.Name)]),
                    " Firewall Rule"),
                new RichDescription()
            );

            if (string.Equals(config[nameof(NetFirewallRuleConfiguration.Exists)], "false", StringComparison.OrdinalIgnoreCase))
            {
                richDesc.LongDescription.AppendContent("does not exist");
                return richDesc;
            }
            else
            {
                richDesc.ShortDescription.AppendContent(
                    " Inbound is ", 
                    new Hilite(config[nameof(NetFirewallRuleConfiguration.Inbound)]), 
                    " and is Allowed ", new Hilite(config[nameof(NetFirewallRuleConfiguration.Allow)]), " on ", 
                    new Hilite(config[nameof(NetFirewallRuleConfiguration.Port)])
                );
            }

            return richDesc;
        }

        protected override Task<PersistedConfiguration> RemoteCollectAsync(IRemoteOperationCollectionContext context)
        {
            this.LogDebug($"Looking for firewall rule \"{this.Template.Name}\"...");
            return Complete(NetFirewallRuleConfiguration.GetRule(this.Template.Name, this.Template.Inbound));
        }

        protected override Task RemoteConfigureAsync(IRemoteOperationExecutionContext context)
        {
            if (this.Template == null)
                throw new InvalidOperationException("Template is not set.");

            this.LogDebug($"Looking for firewall rule \"{this.Template.Name}\"...");
            var rule = NetFirewallRuleConfiguration.GetRule(this.Template.Name, this.Template.Inbound);
            if(!this.Template.Exists)
            {
                if (!rule.Exists)
                {
                    this.LogWarning("Firewall rule does not exist.");
                    return Task.CompletedTask;
                }
                else
                {
                    this.LogDebug($"Deleting \"{this.Template.Name}\" firewall rule...");
                    if (!context.Simulation)
                        rule.DeleteRule();
                }

            }
            else
            {
                if(rule.Exists)
                {
                    this.LogDebug($"Deleting existing \"{this.Template.Name}\" firewall rule...");
                    if (!context.Simulation)
                        rule.DeleteRule();
                    this.LogDebug($"Creating new \"{this.Template.Name}\" firewall rule...");
                    if (!context.Simulation)
                        this.Template.CreateRule();
                }
                else
                {
                    this.LogDebug($"Creating new \"{this.Template.Name}\" firewall rule...");
                    if (!context.Simulation)
                        this.Template.CreateRule();
                }
            }

            return Task.CompletedTask;
        }
    }
}
