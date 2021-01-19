using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.ServiceProcess;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Credentials;
using Inedo.Serialization;
using Inedo.Web;
using Inedo.WindowsServices;
using UsernamePasswordCredentials = Inedo.Extensions.Credentials.UsernamePasswordCredentials;

namespace Inedo.Extensions.Windows.Configurations.Services
{
    [DisplayName("Service")]
    [PersistFrom("Inedo.Otter.Extensions.Configurations.WindowsServices.WindowsServiceConfiguration,OtterCoreEx")]
    [Serializable]
    public sealed class WindowsServiceConfiguration : PersistedConfiguration, IExistential
    {
        [Required]
        [Persistent]
        [ConfigurationKey]
        [ScriptAlias("Name")]
        public string Name { get; set; }
        [Persistent]
        [ScriptAlias("DisplayName")]
        [DisplayName("Display name")]
        public string DisplayName { get; set; }
        [Persistent]
        [ScriptAlias("Description")]
        public string Description { get; set; }
        [Persistent]
        [ScriptAlias("Status")]
        public ServiceControllerStatus? Status { get; set; }
        [Persistent]
        [ScriptAlias("Exists")]
        [DefaultValue(true)]
        public bool Exists { get; set; } = true;
        [Required]
        [Persistent]
        [DisplayName("Path w/ arguments")]
        [ScriptAlias("Path")]
        [Description("The executable path of the service. This field may include any arguments that will be supplied to the executable. Executable paths that include spaces should be wrapped with double-quotes, e.g.: \"C:\\Program Files\\Hdars\\Hdars.Service.exe\" -arg1 -arg2")]
        public string Path { get; set; }
        [Persistent]
        [ScriptAlias("Startup")]
        [Description("Startup type")]
        public Inedo.WindowsServices.ServiceStartMode? StartMode { get; set; }
        [Persistent]
        [ScriptAlias("DelayedStart")]
        [Description("Delayed start")]
        public bool? DelayedStart { get; set; }

        [Category("Log On")]
        [DisplayName("Credentials")]
        [ScriptAlias("Credentials")]
        [Description("The Otter credential name to use as the service's Log On user. If a credential name is specified, the username and password fields will be ignored.")]
        [Persistent]
        [SuggestableValue(typeof(SecureCredentialsSuggestionProvider<UsernamePasswordCredentials>))]
        [IgnoreConfigurationDrift]
        public string CredentialName { get; set; }
        [Category("Log On")]
        [Persistent]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [Description("The user account name to run the service as. If this value is not supplied, NT AUTHORITY\\LocalSystem will be assumed.")]
        public string UserAccount { get; set; }
        [Category("Log On")]
        [Persistent(Encrypted = true)]
        [ScriptAlias("Password")]
        [Description("The password for the account that runs the service. If NT AUTHORITY\\LocalSystem is specified, this field must not have a value set.")]
        [IgnoreConfigurationDrift]
        public string Password { get; set; }

        [Category("Recovery")]
        [Persistent]
        [DisplayName("First failure")]
        [ScriptAlias("FirstFailure")]
        public ServiceControllerActionType? OnFirstFailure { get; set; }
        [Category("Recovery")]
        [Persistent]
        [DisplayName("Second failure")]
        [ScriptAlias("SecondFailure")]
        public ServiceControllerActionType? OnSecondFailure { get; set; }
        [Category("Recovery")]
        [Persistent]
        [DisplayName("Subsequent failures")]
        [ScriptAlias("SubsequentFailures")]
        public ServiceControllerActionType? OnSubsequentFailures { get; set; }
        [Category("Recovery")]
        [Persistent]
        [DisplayName("Restart delay")]
        [ScriptAlias("RestartDelay")]
        public int? RestartDelay { get; set; }
        [Category("Recovery")]
        [Persistent]
        [DisplayName("Run program")]
        [ScriptAlias("OnFailureProgramPath")]
        public string OnFailureProgramPath { get; set; }
        [Category("Recovery")]
        [Persistent]
        [DisplayName("Reboot message")]
        [ScriptAlias("RebootMessage")]
        public string RebootMessage { get; set; }

        // TODO: multiline textbox for array
        [Category("Dependencies")]
        [Persistent]
        [ScriptAlias("Dependencies")]
        public IEnumerable<string> Dependencies { get; set; }

        [Persistent]
        [ScriptAlias("StatusChangeTimeout")]
        [DisplayName("Status change timeout")]
        [DefaultValue(30)]
        [Description("The number of seconds to wait for a server to change between two statuses (e.g. stopped to starting) before raising an error.")]
        [IgnoreConfigurationDrift]
        public TimeSpan StatusChangeTimeout { get; set; } = TimeSpan.FromSeconds(30);

        public static WindowsServiceConfiguration FromService(string serviceName)
        {
            if (string.IsNullOrEmpty(serviceName))
                throw new ArgumentNullException(nameof(serviceName));

            WindowsServiceConfiguration config;

            using (var service = WindowsService.GetService(serviceName))
            {
                if (service == null)
                {
                    return new WindowsServiceConfiguration
                    {
                        Name = serviceName,
                        Exists = false
                    };
                }

                var failureActions = service.FailureActions?.Actions?.Cast<ServiceControllerAction?>();

                config = new WindowsServiceConfiguration
                {
                    Name = service.Name,
                    DisplayName = service.DisplayName,
                    Description = service.Description,
                    Exists = true,
                    Path = service.FileName,
                    StartMode = service.StartMode,
                    UserAccount = service.UserAccountName,
                    DelayedStart = service.DelayedStart,
                    Dependencies = service.Dependencies,
                    OnFirstFailure = failureActions?.ElementAtOrDefault(0)?.Type,
                    OnSecondFailure = failureActions?.ElementAtOrDefault(1)?.Type,
                    OnSubsequentFailures = failureActions?.ElementAtOrDefault(2)?.Type,
                    OnFailureProgramPath = service.FailureActions?.Command,
                    RestartDelay = service.FailureActions?.ResetPeriod,
                    RebootMessage = service.FailureActions?.RebootMessage
                };
            }

            using (var scm = new ServiceController(serviceName))
            {
                config.Status = scm.Status;
            }

            return config;
        }

        public override IReadOnlyDictionary<string, string> GetPropertiesForDisplay(bool hideEncrypted)
        {
            var dic = new Dictionary<string, string>();
            var props = base.GetPropertiesForDisplay(hideEncrypted);
            foreach (var prop in props)
                dic[prop.Key] = prop.Value;

            if (dic.ContainsKey(nameof(this.Dependencies)))
            {
                string csv = string.Join(", ", this.Dependencies ?? Enumerable.Empty<string>());
                if (csv.Length > 0)
                    dic[nameof(this.Dependencies)] = "@(" + csv + ")";
                else
                    dic[nameof(this.Dependencies)] = "None";
            }

            return new ReadOnlyDictionary<string, string>(dic);
        }

        public void SetCredentialProperties(ICredentialResolutionContext context)
        {
            if (!string.IsNullOrEmpty(this.CredentialName))
            {
                if (SecureCredentials.Create(this.CredentialName, context) is not UsernamePasswordCredentials credentials)
                    throw new InvalidOperationException($"{this.CredentialName} is not a " + nameof(UsernamePasswordCredentials));
                this.UserAccount = credentials.UserName;
                this.Password = AH.Unprotect(credentials.Password);
            }
        }

    }
}
