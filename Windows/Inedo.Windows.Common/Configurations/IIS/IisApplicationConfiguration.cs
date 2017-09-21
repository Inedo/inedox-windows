using System;
using System.ComponentModel;
using System.Reflection;
using Inedo.Diagnostics;
using Inedo.Documentation;
#if Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Configurations;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Extensions.Credentials;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Configurations;
using Inedo.BuildMaster.Extensibility.Credentials;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Credentials;
using ILogger = Inedo.Diagnostics.ILogSink;
#endif
using Inedo.Serialization;
using Microsoft.Web.Administration;

namespace Inedo.Extensions.Windows.Configurations.IIS
{
    [DisplayName("IIS Application")]
    [DefaultProperty(nameof(ApplicationPath))]
    [Serializable]
    public sealed class IisApplicationConfiguration : IisConfigurationBase, IHasCredentials
    {

        [DisplayName("Site name")]
        [Description("The name of this site where the application would exist")]
        [ScriptAlias("Site")]
        [ConfigurationKey]
        [Persistent]
        [Required]
        public string SiteName { get; set; }

        [DisplayName("Application path")]
        [Description("The relative URL of the path, such as /hdars")]
        [ScriptAlias("Path")]
        [ConfigurationKey]
        [Persistent]
        [Required]
        public string ApplicationPath { get; set; }

        [DisplayName("Application pool")]
        [Description("The name of the application pool assigned to the application.")]
        [ScriptAlias("AppPool")]
        [Persistent]
        public string ApplicationPoolName { get; set; }

        [DisplayName("Physical path")]
        [Description("Physical path to the content for the application, such as c:\\hdars.")]
        [ScriptAlias("PhysicalPath")]
        [Persistent]
        public string PhysicalPath { get; set; }

        [Category("Impersonation")]
        [DisplayName("Logon method")]
        [Description("Specifies the type of the logon operation to perform when calling LogonUser to acquire the user token impersonated to access the physical path for the application.")]
        [ScriptAlias("LogonMethod")]
        [Persistent]
        public AuthenticationLogonMethod? LogonMethod { get; set; }

        [Category("Impersonation")]
        [DisplayName("Otter credentials")]
        [Description("The Otter credential name to be impersonated when accessing the physical path for the application. If a credential name is specified, the username and password fields will be ignored.")]
        [ScriptAlias("Credentials")]
        [Persistent]
        public string CredentialName { get; set; }

        [Category("Impersonation")]
        [DisplayName("User name")]
        [ScriptAlias("UserName")]
        [MappedCredential(nameof(UsernamePasswordCredentials.UserName))]
        [Persistent]
        public string UserName { get; set; }

        [Category("Impersonation")]
        [DisplayName("Password")]
        [ScriptAlias("Password")]
        [MappedCredential(nameof(UsernamePasswordCredentials.Password))]
        [Persistent]
        public string Password { get; set; }

#if Otter
        public override string ConfigurationKey => this.SiteName + "/" + this.ApplicationPath?.TrimStart('/');
#endif
        public static IisApplicationConfiguration FromMwaApplication(ILogger logger, string siteName, Application app, IisApplicationConfiguration template = null)
        {
            var config = new IisApplicationConfiguration();
            config.SiteName = siteName;
            config.ApplicationPath = app.Path;
            config.ApplicationPoolName = app.ApplicationPoolName;
            config.SetPropertiesFromMwa(logger, app.VirtualDirectories["/"], template);
            return config;
        }

        public static void SetMwaApplication(ILogger logger, IisApplicationConfiguration config, Application app)
        {
            app.Path = config.ApplicationPath;
            app.ApplicationPoolName = config.ApplicationPoolName;
            config.SetPropertiesOnMwa(logger, app.VirtualDirectories["/"]);
        }

        protected override bool SkipTemplateProperty(IisConfigurationBase template, PropertyInfo templateProperty)
        {
            if (templateProperty.Name == nameof(SiteName))
                return true;

            if (templateProperty.Name == nameof(ApplicationPath))
                return true;

            if (templateProperty.Name == nameof(ApplicationPoolName))
                return true;

            if (!string.IsNullOrEmpty((template as IisApplicationConfiguration)?.CredentialName)
                && Attribute.IsDefined(templateProperty, typeof(MappedCredentialAttribute)))
                return false;

            return base.SkipTemplateProperty(template, templateProperty);
        }
    }
}