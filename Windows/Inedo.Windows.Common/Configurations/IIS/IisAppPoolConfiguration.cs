using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
#endif
using Inedo.Serialization;
using Microsoft.Web.Administration;

namespace Inedo.Extensions.Windows.Configurations.IIS
{
    [Serializable]
    [DefaultProperty(nameof(Name))]
    [DisplayName("IIS Application Pool")]
    public sealed class IisAppPoolConfiguration : IisConfigurationBase, IHasCredentials<UsernamePasswordCredentials>
    {
        // https://technet.microsoft.com/en-us/library/cc745955.aspx

#region (General)
        [Required]
        [Persistent]
        [ConfigurationKey]
        [ScriptAlias("Name")]
        [Description("The unique name of the IIS site or application pool.")]
        public string Name { get; set; }

        [DisplayName(".NET CLR version")]
        [Description("The .NET runtime version used by this application pool. Current valid values are \"v4.0\", \"v2.0\", or \"v1.1\".")]
        [ScriptAlias("Runtime")]
        [Persistent]
        public string ManagedRuntimeVersion { get; set; }

        [DisplayName("Enable 32-bit applications")]
        [Description("If set to True for an application pool on a 64-bit operating system, the worker process(es) serving the application pool run in WOW64 (Windows on Windows64) mode. In WOW64 mode, 32-bit processes load only 32-bit applications.")]
        [ScriptAlias("Enable32BitAppOnWin64")]
        [Persistent]
        public bool? Enable32BitAppOnWin64 { get; set; }

        [DisplayName("Managed pipeline mode")]
        [Description("Configures ASP.NET to run in classic mode as an ISAPI extension or in integrated mode where managed code is integrated into the request-processing pipeline.")]
        [ScriptAlias("Pipeline")]
        [Persistent]
        public ManagedPipelineMode? ManagedPipelineMode { get; set; }

        [DisplayName("Start automatically")]
        [Description("If True, the application pool starts on creation or when IIS starts. Starting an application pool sets this property to True. Stopping an application sets this property to False.")]
        [ScriptAlias("AutoStart")]
        [Persistent]
        public bool? AutoStart { get; set; }

        [DisplayName("Queue length")]
        [Description("Maximum number of requests that Http.sys queues for the application pool. When the queue is full, new requests receive a 503 \"Service Unavailable\" response")]
        [ScriptAlias("QueueLength")]
        [Persistent]
        public long? QueueLength { get; set; }

        [DisplayName("State")]
        [Description("Sets the running state for the application pool.")]
        [ScriptAlias("State")]
        [Persistent]
        public IisObjectState? Status { get; set; }
#endregion

#region Identity
        [Category("Identity")]
        [DisplayName("Identity type")]
        [Description("Configures the application pool to run as a built-in account, such as Network Service (recommended), Local Service, or as a specific user identity.")]
        [ScriptAlias("IdentityType")]
        [Persistent]
        public ProcessModelIdentityType? ProcessModel_IdentityType { get; set; }

        [Category("Identity")]
        [DisplayName("Otter credentials")]
        [Description("The Otter credential name to use for the application pool's identity. If a credential name is specified, the username and password fields will be ignored.")]
        [ScriptAlias("Credentials")]
        [Persistent]
        public string CredentialName { get; set; }

        [Category("Identity")]
        [DisplayName("User name")]
        [ScriptAlias("UserName")]
        [Description("Configures the application pool to run as a built-in account, such as Network Service (recommended), Local Service, or as a specific user identity.")]
        [MappedCredential(nameof(UsernamePasswordCredentials.UserName))]
        [Persistent]
        public string ProcessModel_UserName { get; set; }

        [Category("Identity")]
        [DisplayName("Password")]
        [ScriptAlias("Password")]
        [MappedCredential(nameof(UsernamePasswordCredentials.Password))]
        [Persistent]
        public string ProcessModel_Password { get; set; }
#endregion

#region CPU
        [Category("CPU")]
        [DisplayName("Limit (percent)")]
        [Description("Configures the maximum percentage of CPU time (in 1/1000ths of a percent) that the worker processes in an application pool are allowed to consume over a period of time as indicated by the Limit Interval setting (resetInterval property). If the limit set by Limit (limit property) is exceeded, the event is written to the event log and an optional set of events can be triggered or determined by the Limit Action setting (action property). Setting the value of Limit to 0 disables limiting the worker processes to a percentage of CPU time.")]
        [ScriptAlias("CpuLimit")]
        [Persistent]
        public long? Cpu_Limit { get; set; }

        [Category("CPU")]
        [DisplayName("Limit action")]
        [Description("If set to NoAction, an event log entry is generated. If set to KillW3WP, the application pool is shut down for the duration of the reset interval and an event log entry is generated.")]
        [ScriptAlias("CpuAction")]
        [Persistent]
        public ProcessorAction? Cpu_Action { get; set; }

        [Category("CPU")]
        [DisplayName("Limit interval (minutes)")]
        [Description("Specifies the reset period (in minutes) for CPU monitoring and throttling limits on the application pool. When the number of minutes elapsed since the last process accounting reset equals the number specified by this property, IIS resets the CPU timers for both the logging and limit intervals. Setting the value of Limit Interval to 0 disables CPU monitoring.")]
        [ScriptAlias("CpuResetInterval")]
        [Persistent]
        public TimeSpan? Cpu_ResetInterval { get; set; }

        [Category("CPU")]
        [DisplayName("Processor affinity enabled")]
        [Description("If True, Processor Affinity Enabled forces the worker process(es) serving this application pool to run on specific CPUs. This enables sufficient use of CPU caches on multiprocessor servers.")]
        [ScriptAlias("CpuSmpAffinitized")]
        [Persistent]
        public bool? Cpu_SmpAffinitized { get; set; }

        [Category("CPU")]
        [DisplayName("Processor affinity mask")]
        [Description("Hexadecimal mask that forces the worker process(es) for this application pool to run on a specific CPU. If processor affinity is enabled, a value of 0 causes an error condition.")]
        [ScriptAlias("CpuSmpProcessorAffinityMask")]
        [Persistent]
        public long? Cpu_SmpProcessorAffinityMask { get; set; }

        [Category("CPU")]
        [DisplayName("Processor affinity (64-bit)")]
        [Description("Hexadecimal mask that forces the worker process(es) for this application pool to run on a specific CPU. If processor affinity is enabled, a value of 0 causes an error condition.")]
        [ScriptAlias("CpuSmpProcessorAffinityMask2")]
        [Persistent]
        public long? Cpu_SmpProcessorAffinityMask2 { get; set; }
#endregion

#region Process Model
        [Category("Process Model")]
        [DisplayName("Idle time-out (minutes)")]
        [Description("Amount of time (in minutes) a worker process remains idle before it shuts down. A worker process is idle if it is not processing requests and no new requests are received.")]
        [ScriptAlias("IdleTimeout")]
        [TimeSpanUnit(TimeSpanUnit.Minutes)]
        [Persistent]
        public TimeSpan? ProcessModel_IdleTimeout { get; set; }

        [Category("Process Model")]
        [DisplayName("Load user profile")]
        [Description("Specifies whether IIS loads the user profile for an application pool identity. When set to True, IIS loads the user profile for the application pool identity. Set to False when you require IIS 6.0 behavior.")]
        [ScriptAlias("LoadUserProfile")]
        [Persistent]
        public bool? ProcessModel_LoadUserProfile { get; set; }

        [Category("Process Model")]
        [DisplayName("Max worker processes")]
        [Description("Maximum number of worker processes permitted to service requests for the application pool. If this number is greater than 1, the application pool is called a Web garden.")]
        [ScriptAlias("MaxProcesses")]
        [Persistent]
        public long? ProcessModel_MaxProcesses { get; set; }

        [Category("Process Model")]
        [DisplayName("Ping enabled")]
        [Description("If True, the worker process(es) serving this application pool are pinged periodically to ensure that they are still responsive. This process is called health monitoring.")]
        [ScriptAlias("PingingEnabled")]
        [Persistent]
        public bool? ProcessModel_PingingEnabled { get; set; }

        [Category("Process Model")]
        [DisplayName("Ping max response time (seconds)")]
        [Description("Maximum time (in seconds) that a worker process is given to respond to a health monitoring ping. If the worker process does not respond, it is terminated.")]
        [ScriptAlias("PingResponseTime")]
        [Persistent]
        public TimeSpan? ProcessModel_PingResponseTime { get; set; }

        [Category("Process Model")]
        [DisplayName("Ping period (seconds)")]
        [Description("Period of time (in seconds) between health monitoring pings sent to the worker process(es) serving this application pool.")]
        [ScriptAlias("PingInterval")]
        [Persistent]
        public TimeSpan? ProcessModel_PingInterval { get; set; }

        [Category("Process Model")]
        [DisplayName("Shutdown time limit (seconds)")]
        [Description("Period of time (in seconds) a worker process is given to finish processing requests and shut down. If the worker process exceeds the shutdown time limit, it is terminated.")]
        [ScriptAlias("ShutdownTimeLimit")]
        [Persistent]
        public TimeSpan? ProcessModel_ShutdownTimeLimit { get; set; }

        [Category("Process Model")]
        [DisplayName("Startup time limit (seconds)")]
        [Description("Period of time (in seconds) a worker process is given to start up and initialize. If the worker process initialization exceeds the startup time limit, it is terminated.")]
        [ScriptAlias("StartupTimeLimit")]
        [Persistent]
        public TimeSpan? ProcessModel_StartupTimeLimit { get; set; }
#endregion

#region Process Orphaning 
        [Category("Process Orphaning")]
        [DisplayName("Process orphaning enabled")]
        [Description("If True, an unresponsive worker process is abandoned (orphaned) instead of terminated. This feature can be used to debug a worker process failure.")]
        [ScriptAlias("OrphanWorkerProcess")]
        [Persistent]
        public bool? Failure_OrphanWorkerProcess { get; set; }

        [Category("Process Orphaning")]
        [DisplayName("Process orphaning executable")]
        [Description("Executable to run when a worker process is abandoned (orphaned). For example, C:\\dbgtools\ntsd.exe would invoke NTSD to debug a worker process failure.")]
        [ScriptAlias("OrphanActionExe")]
        [Persistent]
        public string Failure_OrphanActionExe { get; set; }

        [Category("Process Orphaning")]
        [DisplayName("Executable parameters")]
        [Description("Parameters for the executable that is run when a worker process is abandoned (orphaned). For example, -g –p %1% is appropriate if the NTSD is the executable invoked for debugging worker process failures.")]
        [ScriptAlias("OrphanActionParams")]
        [Persistent]
        public string Failure_OrphanActionParams { get; set; }
#endregion

#region Rapid Fail Protection 
        [Category("Rapid Fail Protection")]
        [DisplayName("Service unavailable response type")]
        [Description("If set to HttpLevel and the application pool is stopped, Http.sys returns an HTTP 503 error. If set to TcpLevel, Http.sys resets the connection. This is useful if the load balancer recognizes one of the response types and subsequently redirects it.")]
        [ScriptAlias("LoadBalancerCapabilities")]
        [Persistent]
        public LoadBalancerCapabilities? Failure_LoadBalancerCapabilities { get; set; }

        [Category("Rapid Fail Protection")]
        [DisplayName("Rapid fail protection enabled")]
        [Description("If True, the application pool is shut down if there are a specified number of worker process failures (Maximum Failures) within a specified period (Failure Interval). By default, an application pool is shut down if there are five failures in a five minute period.")]
        [ScriptAlias("RapidFailProtection")]
        [Persistent]
        public bool? Failure_RapidFailProtection { get; set; }

        [Category("Rapid Fail Protection")]
        [DisplayName("Failure interval (minutes)")]
        [Description("The time interval (in minutes) during which the specified number of worker process failures (Maximum Failures) must occur before the application pool is shut down by Rapid Fail Protection.")]
        [ScriptAlias("RapidFailProtectionInterval")]
        [TimeSpanUnit(TimeSpanUnit.Minutes)]
        [Persistent]
        public TimeSpan? Failure_RapidFailProtectionInterval { get; set; }

        [Category("Rapid Fail Protection")]
        [DisplayName("Maximum failures")]
        [Description("Maximum number of worker process failures permitted before the application pool is shut down by Rapid Fail Protection.")]
        [ScriptAlias("RapidFailProtectionMaxCrashes")]
        [Persistent]
        public long? Failure_RapidFailProtectionMaxCrashes { get; set; }

        [Category("Rapid Fail Protection")]
        [DisplayName("Shutdown executable")]
        [Description("Executable to run when an application pool is shut down by Rapid Fail Protection.This can be used to configure a load balancer to redirect traffic for this application to another server.")]
        [ScriptAlias("AutoShutdownExe")]
        [Persistent]
        public string Failure_AutoShutdownExe { get; set; }

        [Category("Rapid Fail Protection")]
        [DisplayName("Shutdown executable parameters")]
        [Description("Parameters for the executable to run when an application pool is shut down by Rapid Fail Protection.")]
        [ScriptAlias("AutoShutdownParams")]
        [Persistent]
        public string Failure_AutoShutdownParams { get; set; }
#endregion

#region Recycling 
        [Category("Recycling")]
        [DisplayName("Disable overlapped recycle")]
        [Description("If True, when the application pool recycles, the existing worker process exits before another worker process is created. Set to True if the worker process loads an application that does not support multiple instances.")]
        [ScriptAlias("DisallowOverlappingRotation")]
        [Persistent]
        public bool? Recycling_DisallowOverlappingRotation { get; set; }

        [Category("Recycling")]
        [DisplayName("Disable for config changes")]
        [Description("If True, the application pool does not recycle when its configuration is changed.")]
        [ScriptAlias("DisallowRotationOnConfigChange")]
        [Persistent]
        public bool? Recycling_DisallowRotationOnConfigChange { get; set; }

        //public RecyclingLogEventOnRecycle Recycling_LogEventOnRecycle { get; set; }

        [Category("Recycling")]
        [DisplayName("Private memory limit (KB)")]
        [Description("Maximum amount of private memory (in KB) a worker process can consume before causing the application pool to recycle. A value of 0 means there is no limit.")]
        [ScriptAlias("PeriodicRestartPrivateMemory")]
        [Persistent]
        public long? Recycling_PeriodicRestart_PrivateMemory { get; set; }

        [Category("Recycling")]
        [DisplayName("Regular time interval (minutes)")]
        [Description("Period of time (in minutes) after which an application pool recycles. A value of 0 means the application pool does not recycle at a regular interval.")]
        [ScriptAlias("PeriodicRestartTime")]
        [TimeSpanUnit(TimeSpanUnit.Minutes)]
        [Persistent]
        public TimeSpan? Recycling_PeriodicRestart_Time { get; set; }

        [Category("Recycling")]
        [DisplayName("Request Limit")]
        [Description("Maximum number of requests an application pool can process before it is recycled. A value of 0 means the application pool can process an unlimited number of requests.")]
        [ScriptAlias("PeriodicRestartRequests")]
        [Persistent]
        public long? Recycling_PeriodicRestart_Requests { get; set; }
        
        //public ScheduleCollection Recycling_PeriodicRestart_Schedule { get; }

        [Category("Recycling")]
        [DisplayName("Virtual memory limit (KB)")]
        [Description("Maximum amount of virtual memory (in KB) a worker process can consume before causing the application pool to recycle. A value of 0 means there is no limit.")]
        [ScriptAlias("PeriodicRestartMemory")]
        [Persistent]
        public long? Recycling_PeriodicRestart_Memory { get; set; }
#endregion

        public static IisAppPoolConfiguration FromMwaApplicationPool(ILogger logger, ApplicationPool pool, IisAppPoolConfiguration template = null)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (pool == null)
                throw new ArgumentNullException(nameof(pool));

            var config = new IisAppPoolConfiguration();
            config.Name = pool.Name;
            config.Status = (IisObjectState)pool.State;
            if (template == null)
                return config;

            var templateProperties = Persistence.GetPersistentProperties(template.GetType(), false)
                .Where(p => Attribute.IsDefined(p, typeof(ScriptAliasAttribute)));

            foreach (var templateProperty in templateProperties)
            {
                if (SkipTemplateProperty(template, templateProperty))
                    continue;

                var mappedProperty = FindMatchingProperty(templateProperty.Name.Split('_'), pool);
                if (mappedProperty != null)
                    templateProperty.SetValue(config, mappedProperty.GetValue());
                else
                    logger.LogWarning($"Matching MWA property \"{templateProperty.Name}\" was not found.");
            }

            return config;
        }

        private static bool SkipTemplateProperty(IisAppPoolConfiguration template, PropertyInfo templateProperty)
        {
            if (templateProperty.Name == nameof(Status))
                return false;

            var value = templateProperty.GetValue(template);
            if (value != null)
                return false;

            if (!string.IsNullOrEmpty(template.CredentialName) && Attribute.IsDefined(templateProperty, typeof(MappedCredentialAttribute)))
                return false;

            return true;
        }

        public static void SetMwaApplicationPool(ILogger logger, IisAppPoolConfiguration config, ApplicationPool pool)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (pool == null)
                throw new ArgumentNullException(nameof(pool));

            var configProperties = Persistence.GetPersistentProperties(config.GetType(), false)
                .Where(p => Attribute.IsDefined(p, typeof(ScriptAliasAttribute)));

            foreach (var configProperty in configProperties)
            {
                if(SkipTemplateProperty(config, configProperty))
                    continue;

                object value = configProperty.GetValue(config);
                
                var mappedProperty = FindMatchingProperty(configProperty.Name.Split('_'), pool);
                if (mappedProperty != null)
                    mappedProperty.SetValue(value);
                else
                    logger.LogWarning($"Matching MWA property \"{configProperty.Name}\" was not found.");
            }
        }

        private static MappedProperty FindMatchingProperty(IList<string> propertyNames, object propertyInstance)
        {
            if (propertyNames.Count == 0)
                return null;

            string name = propertyNames.First();
            var appPoolProperty = propertyInstance.GetType().GetProperty(name);
            if (appPoolProperty == null)
                return null;

            var appPoolPropertyInstance = appPoolProperty.GetValue(propertyInstance);

            return FindMatchingProperty(propertyNames.Skip(1).ToArray(), appPoolPropertyInstance)
                ?? new MappedProperty(propertyInstance, appPoolProperty);
        }

        private class MappedProperty
        {
            public MappedProperty(object instance, PropertyInfo prop)
            {
                if (instance == null)
                    throw new ArgumentNullException(nameof(instance));
                if (prop == null)
                    throw new ArgumentNullException(nameof(prop));

                this.Instance = instance;
                this.MwaAppPoolProperty = prop;
            }

            public object Instance { get; }
            public PropertyInfo MwaAppPoolProperty { get; }

            public void SetValue(object value)
            {
                this.MwaAppPoolProperty.SetValue(this.Instance, value);
            }

            public object GetValue()
            {
                return this.MwaAppPoolProperty.GetValue(this.Instance);
            }
        }
    }
}