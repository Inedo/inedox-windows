using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.ServiceProcess;
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
#endif
using Inedo.Serialization;
using Inedo.WindowsServices;

namespace Inedo.Extensions.Windows.Configurations.Services
{
    [DisplayName("Service")]
    [PersistFrom("Inedo.Otter.Extensions.Configurations.WindowsServices.WindowsServiceConfiguration,OtterCoreEx")]
    [Serializable]
    public sealed class WindowsServiceConfiguration : PersistedConfiguration, IExistential, IHasCredentials<UsernamePasswordCredentials>
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
        public string CredentialName { get; set; }
        [Category("Log On")]
        [Persistent]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [MappedCredential(nameof(UsernamePasswordCredentials.UserName))]
        [Description("The user account name to run the service as. If this value is not supplied, NT AUTHORITY\\LocalSystem will be assumed.")]
        public string UserAccount { get; set; }
        [Category("Log On")]
        [Persistent]
        [ScriptAlias("Password")]
        [MappedCredential(nameof(UsernamePasswordCredentials.Password))]
        [Description("The password for the account that runs the service. If NT AUTHORITY\\LocalSystem is specified, this field must not have a value set.")]
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
#if Otter
        [IgnoreConfigurationDrift]
#endif
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
    }
}
