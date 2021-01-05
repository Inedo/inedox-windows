using System;
using System.ComponentModel;
using System.Reflection;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Credentials;
using Inedo.Extensions.Credentials;
using Inedo.Serialization;
using Inedo.Web;
using Microsoft.Web.Administration;
using UsernamePasswordCredentials = Inedo.Extensions.Credentials.UsernamePasswordCredentials;

namespace Inedo.Extensions.Windows.Configurations.IIS
{
    [Serializable]
    [SlimSerializable]
    [DisplayName("IIS Application")]
    [DefaultProperty(nameof(ApplicationPath))]
    public sealed class IisApplicationConfiguration : IisConfigurationBase
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
        [SuggestableValue(typeof(SecureCredentialsSuggestionProvider<UsernamePasswordCredentials>))]
        public string CredentialName { get; set; }

        [Category("Impersonation")]
        [DisplayName("User name")]
        [ScriptAlias("UserName")]
        [Persistent]
        public string UserName { get; set; }

        [Category("Impersonation")]
        [DisplayName("Password")]
        [ScriptAlias("Password")]
        [Persistent(Encrypted = true)]
        public string Password { get; set; }
        public static IisApplicationConfiguration FromMwaApplication(ILogSink logger, string siteName, Application app, IisApplicationConfiguration template = null)
        {
            var config = new IisApplicationConfiguration
            {
                SiteName = siteName,
                ApplicationPath = app.Path,
                ApplicationPoolName = app.ApplicationPoolName
            };
            config.SetPropertiesFromMwa(logger, app.VirtualDirectories["/"], template);
            return config;
        }

        public static void SetMwaApplication(ILogSink logger, IisApplicationConfiguration config, Application app)
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
                && (templateProperty.Name == nameof(UserName) || templateProperty.Name == nameof(Password)))
                return false;

            return base.SkipTemplateProperty(template, templateProperty);
        }
        public void SetCredentialProperties(ICredentialResolutionContext context)
        {
            if (string.IsNullOrEmpty(this.CredentialName))
            {

                if (SecureCredentials.Create(this.CredentialName, context) is not UsernamePasswordCredentials credentials)
                    throw new InvalidOperationException($"{this.CredentialName} is not a " + nameof(UsernamePasswordCredentials));
                this.UserName = credentials.UserName;
                this.Password = AH.Unprotect(credentials.Password);
            }
        }

    }
}
