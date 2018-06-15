using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensions.Windows.SuggestionProviders;
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

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Persistent]
        [ScriptAlias("Binding")]
        [DisplayName("Binding")]
        [IgnoreConfigurationDrift]
        public string BindingInformation { get; set; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Persistent]
        [ScriptAlias("Protocol")]
        [DisplayName("Protocol")]
        [IgnoreConfigurationDrift]
        public string BindingProtocol { get; set; }

        [Persistent]
        [SuggestableValue(typeof(LegacyBindingSuggestionProvider))]
        [ScriptAlias("Bindings")]
        [DisplayName("Bindings")]
        [FieldEditMode(FieldEditMode.Multiline)]
        [Description(@"Bindings are entered as a list of maps, e.g.:<br/><pre style=""white-space: pre-wrap;"">@(%(IPAddress: 192.0.2.100, Port: 80, HostName: example.com, Protocol: http), %(IPAddress: 192.0.2.101, Port: 443, HostName: secure.example.com, Protocol: https, CertificateStoreName: WebHosting, CertificateHash: 51599BF2909EA984793481F0DF946C57E4FD5DEA, ServerNameIndication: true, UseCentralizedStore: false))</pre>")]
        public IEnumerable<IReadOnlyDictionary<string, RuntimeValue>> Bindings { get; set; }

        public static IisSiteConfiguration FromMwaSite(ILogSink logger, Site site, IisSiteConfiguration template = null)
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

            if (template?.BindingInformation != null && template?.Bindings != null)
            {
                logger.LogWarning("Template configuration has both Binding and Bindings specified; the Binding and Protocol properties "
                    + "will be ignored and should be removed from the operation via Text Mode of the Plan Editor.");
            }

            var siteBindings = site.Bindings
                .Select(b => BindingInfo.FromBindingInformation(b.BindingInformation, b.Protocol, b.CertificateStoreName, b.CertificateHash, b))
                .ToArray();

            if (siteBindings.Length == 0)
            {
                logger.LogWarning("Site does not have a Binding configured.");
            }
            else
            {
                if (template == null || (template.Bindings != null || template.BindingInformation != null))
                {
                    config.Bindings = siteBindings.Select(b => b.ToDictionary()).ToArray();
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
            if (templateBindings != null)
            {
                logger.LogDebug("Clearing bindings...");
                site.Bindings.Clear();
                logger.LogDebug("Setting bindings...");
                foreach (var binding in templateBindings)
                {
                    Binding iisBinding;

                    if (binding.CertificateHash.Length > 0)
                        iisBinding = site.Bindings.Add(binding.BindingInformation, binding.CertificateHash, binding.CertificateStoreName);
                    else
                        iisBinding = site.Bindings.Add(binding.BindingInformation, binding.Protocol);

                    iisBinding.SetAttributeValue("sslFlags", (int)binding.SslFlags);
                }
            }
        }

        public override IReadOnlyDictionary<string, string> GetPropertiesForDisplay(bool hideEncrypted)
        {
            var dic = new Dictionary<string, string>();
            var props = base.GetPropertiesForDisplay(hideEncrypted);
            foreach (var prop in props)
                dic[prop.Key] = prop.Value;

            if (this.Bindings != null)
                dic[nameof(this.Bindings)] = string.Join(Environment.NewLine, this.Bindings.Select(b => BindingInfo.FromMap(b)));

            return new ReadOnlyDictionary<string,string>(dic);
        }

        public override ComparisonResult Compare(PersistedConfiguration other)
        {
            var result = base.Compare(other);

            var differences = result.Differences.Where(d => d.Name != nameof(this.Bindings)).ToList();

            if (this.Bindings == null && this.BindingInformation != null)
                this.Bindings = new[] { BindingInfo.FromBindingInformation(this.BindingInformation, this.BindingProtocol).ToDictionary() };

            if (this.Bindings == null)
                return new ComparisonResult(differences);

            var otherBindings = ((IisSiteConfiguration)other).Bindings;
            if (otherBindings == null && ((IisSiteConfiguration)other).BindingInformation != null)
                otherBindings = new[] { BindingInfo.FromBindingInformation(((IisSiteConfiguration)other).BindingInformation, ((IisSiteConfiguration)other).BindingProtocol).ToDictionary() };

            if (otherBindings == null)
                otherBindings = Enumerable.Empty<IReadOnlyDictionary<string, RuntimeValue>>();

            var template = this.Bindings.Select(b => BindingInfo.FromMap(b)).ToHashSet();
            var actual = otherBindings.Select(b => BindingInfo.FromMap(b)).ToHashSet();

            if (template.SetEquals(actual))
                return new ComparisonResult(differences);

            var diff = new Difference(nameof(this.Bindings), string.Join("; ", template), string.Join("; ", actual));
            differences.Add(diff);

            return new ComparisonResult(differences);
        }

        private static BindingInfo[] GetTemplateBindings(IisSiteConfiguration config)
        {
            if (config.BindingInformation != null && config.BindingProtocol != null && config.Bindings == null)
            {
                // use legacy operation property values only if "Binding" script alias is present and "Bindings" is not
                var legacyBindingInfo = BindingInfo.FromBindingInformation(config.BindingInformation, config.BindingProtocol);
                return new[] { legacyBindingInfo };
            }
            else
            {
                if (config.Bindings == null)
                    return null;

                var templateBindings =
                        (from b in config.Bindings
                         let info = BindingInfo.FromMap(b)
                         where info != null
                         select info)
                    .ToArray();

                return templateBindings;
            }
        }
    }
}
