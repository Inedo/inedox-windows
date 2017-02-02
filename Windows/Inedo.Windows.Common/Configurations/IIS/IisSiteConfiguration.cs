using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Configurations;
using Inedo.Otter.Extensions;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Configurations;
using Inedo.BuildMaster.Web;
#endif
using Inedo.Serialization;
using Microsoft.Web.Administration;

namespace Inedo.Extensions.Windows.Configurations.IIS
{
    [DisplayName("IIS Site")]
    [Description("Describes an IIS Site with a single application and a single virtual directory.")]
    [DefaultProperty(nameof(Name))]
    [PersistFrom("Inedo.Otter.Extensions.Configurations.IIS.IisSiteConfiguration,OtterCoreEx")]
    [Serializable]
    public sealed class IisSiteConfiguration : IisConfigurationBase, IMissingPersistentPropertyHandler
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
        [ScriptAlias("Bindings")]
        [DisplayName("Bindings")]
        [FieldEditMode(FieldEditMode.Multiline)]
        #region [Description]...
        [Description(@"Bindings are entered as a list of maps, e.g.:<br />
<pre>
@(
    %(
        IPAddress: 192.0.2.100, 
        Port: 80, 
        HostName: example.com, 
        Protocol: http
    ),
    %(
        IPAddress: 192.0.2.101, 
        Port: 443, 
        HostName: secure.example.com,
        Protocol: https,
        CertificateStoreName: WebHosting,
        CertificateHash: 51599BF2909EA984793481F0DF946C57E4FD5DEA
    )
)
</pre>
")]
        #endregion
        public IEnumerable<IReadOnlyDictionary<string, RuntimeValue>> Bindings { get; set; }

        public static IisSiteConfiguration FromMwaSite(ILogger logger, Site site, IisSiteConfiguration template = null)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (site == null)
                throw new ArgumentNullException(nameof(site));

            var config = new IisSiteConfiguration();
            config.Name = site.Name;

            var app = site.Applications.FirstOrDefault();
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
                    if (template == null || template.ApplicationPoolName != null)
                        config.ApplicationPoolName = app.ApplicationPoolName;

                    if (template == null || template.VirtualDirectoryPhysicalPath != null)
                        config.VirtualDirectoryPhysicalPath = vdir.PhysicalPath;
                }
            }

            var siteBindings = site.Bindings
                .Select(b => BindingInfo.FromBindingInformation(b.BindingInformation, b.Protocol, b.CertificateStoreName, b.CertificateHash))
                .ToArray();

            if (siteBindings.Length == 0)
            {
                logger.LogWarning("Site does not have a Binding configured.");
            }
            else
            {
                if (template == null || template.Bindings != null)
                {
                    config.Bindings = siteBindings.Select(b => b.ToDictionary()).ToArray();
                }
            }

            return config;
        }

        public static void SetMwaSite(ILogger logger, IisSiteConfiguration config, Site site)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (site == null)
                throw new ArgumentNullException(nameof(site));

            if (site.Applications.Count > 1)
                logger.LogWarning("Site has more than one Application defined; this will only configure the first one.");

            var app = site.Applications.FirstOrDefault();
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

            var templateBindings = GetTemplateBindings(config);

            logger.LogDebug("Clearing bindings...");
            site.Bindings.Clear();
            logger.LogDebug("Setting bindings...");
            foreach (var binding in templateBindings)
            {
                if (binding.CertificateHash.Length > 0)
                    site.Bindings.Add(binding.BindingInformation, binding.CertificateHash, binding.CertificateStoreName);
                else
                    site.Bindings.Add(binding.BindingInformation, binding.Protocol);
            }
        }

#if Otter
        public override IDictionary<string, string> GetPropertiesForDisplay(bool hideEncrypted)
        {
            var props = base.GetPropertiesForDisplay(hideEncrypted);

            props[nameof(this.Bindings)] = string.Join(Environment.NewLine, this.Bindings.Select(b => BindingInfo.FromMap(b)));

            return props;
        }

        public override ComparisonResult Compare(PersistedConfiguration other)
        {
            var result = base.Compare(other);

            var differences = result.Differences.Where(d => d.Name != nameof(this.Bindings)).ToList();

            if (this.Bindings == null)
                return new ComparisonResult(differences);

            var template = this.Bindings.Select(b => BindingInfo.FromMap(b)).ToHashSet();
            var actual = ((IisSiteConfiguration)other).Bindings.Select(b => BindingInfo.FromMap(b)).ToHashSet();

            if (template.SetEquals(actual))
                return new ComparisonResult(differences);

            var diff = new Difference(nameof(this.Bindings), string.Join("; ", template), string.Join("; ", actual));
            differences.Add(diff);

            return new ComparisonResult(differences);
        }
#endif

        private static BindingInfo[] GetTemplateBindings(IisSiteConfiguration config)
        {
            var templateBindings =
                    (from b in config.Bindings ?? Enumerable.Empty<IReadOnlyDictionary<string, RuntimeValue>>()
                     let info = BindingInfo.FromMap(b)
                     where info != null
                     select info)
                .ToArray();

            return templateBindings;
        }

        public void OnDeserializedMissingProperties(IReadOnlyDictionary<string, string> missingProperties)
        {
            string info = missingProperties.GetValueOrDefault("BindingInformation");
            string protocol = missingProperties.GetValueOrDefault("BindingProtocol");
            if (info == null || protocol == null)
                return;

            var legacyBindingInfo = BindingInfo.FromBindingInformation(info, protocol, null, null);

            this.Bindings = new[] { legacyBindingInfo.ToDictionary() };
        }
    }
}
