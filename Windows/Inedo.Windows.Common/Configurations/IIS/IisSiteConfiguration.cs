using System;
using System.ComponentModel;
using System.Linq;
using Inedo.Diagnostics;
using Inedo.Documentation;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Configurations;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Configurations;
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

        [Persistent]
        [ScriptAlias("Protocol")]
        [DisplayName("Protocol")]
        [Description("The HTTP protocol used by the site. Valid values are \"http\" or \"https\".")]
        public string BindingProtocol { get; set; }

        [Persistent]
        [ScriptAlias("Binding")]
        [DisplayName("Binding")]
        [Description("The value of this property is a colon-delimited string of the format: «IPAddress»:«Port»:«HostName» - You may leave the host name blank. "
                     + "You can set the IP address to \"*\" to indicate that the site is bound to all IP addresses. A port number is required.")]
        public string BindingInformation { get; set; }

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

            var bind = site.Bindings.FirstOrDefault();
            if (bind == null)
            {
                logger.LogWarning("Site does not have a Binding configured.");
            }
            else
            {
                if (template == null || template.BindingProtocol != null)
                    config.BindingProtocol = bind.Protocol;

                if (template == null || template.BindingInformation != null)
                    config.BindingInformation = bind.BindingInformation;
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

            if (site.Bindings.Count > 1)
                logger.LogWarning("Site has more than one Binding defined; this will only configure the first one.");

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

            var bind = site.Bindings.FirstOrDefault();
            if (bind == null)
            {
                logger.LogDebug("Site does not have a Binding; creating Binding...");
                bind = site.Bindings.Add(config.BindingInformation, config.BindingProtocol);
            }

            if (config.BindingProtocol != null)
                bind.Protocol = config.BindingProtocol;

            if (config.BindingInformation != null)
                bind.BindingInformation = config.BindingInformation;
        }
    }
}
