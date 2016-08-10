using System;
using System.ComponentModel;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
#if Otter
using Inedo.Otter.Documentation;
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Configurations;
using Inedo.Otter.Extensibility.Operations;
#elif BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
#endif
using Inedo.Extensions.Windows.Configurations.Services;
using Inedo.WindowsServices;

namespace Inedo.Extensions.Windows.Operations.Services
{
    [DisplayName("Ensure Service")]
    [Description("Ensures the configuration of a Windows service on a server.")]
    [ScriptAlias("Ensure-Service")]
    [Serializable]
    [ScriptNamespace(Namespaces.Windows, PreferUnqualified = true)]
    [Tag(Tags.Services)]
    [Example(@"
# ensures the HdarsSvc is present on the server using the HdarsSvc credentials in Otter
Windows::Ensure-Service
(
    Name: HdarsSvc,
    DisplayName: HDARS Console Log Service,
    Status: Running,
    Path: E:\Services\Hdars.Service.exe,
    Startup: Auto,
    Credentials: HdarsSvc,
    FirstFailure: Restart
);
")]
    public sealed class EnsureServiceOperation : RemoteEnsureOperation<WindowsServiceConfiguration>
    {
        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            var richDesc = new ExtendedRichDescription(
                new RichDescription(
                    "Ensure ",
                    new Hilite(config[nameof(WindowsServiceConfiguration.Name)]),
                    " (",
                    config[nameof(WindowsServiceConfiguration.DisplayName)],
                    ") ",
                    " Service"),
                new RichDescription()
            );

            var desc = richDesc.LongDescription;

            if (string.Equals(config[nameof(WindowsServiceConfiguration.Exists)], "false", StringComparison.OrdinalIgnoreCase))
            {
                richDesc.LongDescription.AppendContent("does not exist");
                return richDesc;
            }
            else
            {
                if (!string.IsNullOrEmpty(config[nameof(WindowsServiceConfiguration.Path)]))
                    desc.AppendContent(" at path ", new DirectoryHilite(config[nameof(WindowsServiceConfiguration.Path)]));

                if (!string.IsNullOrEmpty(config[nameof(WindowsServiceConfiguration.Status)]))
                    richDesc.ShortDescription.AppendContent(" is ", new Hilite(config[nameof(WindowsServiceConfiguration.Status)]));
            }

            return richDesc;
        }

#if Otter
        protected override Task<PersistedConfiguration> RemoteCollectAsync(IRemoteOperationExecutionContext context)
        {
            this.LogDebug($"Looking for service \"{this.Template.Name}\"...");
            return Complete(WindowsServiceConfiguration.FromService(this.Template.Name));
        }
#endif

        protected async override Task RemoteConfigureAsync(IRemoteOperationExecutionContext context)
        {
            if (this.Template == null)
                throw new InvalidOperationException("Template is not set.");

            this.LogDebug($"Looking for service \"{this.Template.Name}\"...");

            bool userWasChanged = false;

            using (var service = GetOrCreateService(!context.Simulation))
            {
                if (this.Template.Exists)
                {
                    if (service == null)
                    {
                        // simulation
                        this.LogInformation($"{this.Template.Name} service does not exist.");
                        return;
                    }

                    if (this.Template.DisplayName != null && this.Template.DisplayName != service.DisplayName)
                        service.DisplayName = this.Template.DisplayName;

                    if (this.Template.Description != null && this.Template.Description != service.Description)
                        service.Description = this.Template.Description;

                    if (this.Template.Path != null && this.Template.Path != service.FileName)
                        service.FileName = this.Template.Path;

                    if (this.Template.StartMode != null && this.Template.StartMode != service.StartMode)
                        service.StartMode = this.Template.StartMode.Value;

                    if (this.Template.DelayedStart != null && this.Template.DelayedStart != service.DelayedStart)
                        service.DelayedStart = this.Template.DelayedStart.Value;

                    if (this.Template.UserAccount != null && this.Template.UserAccount != service.UserAccountName)
                    {
                        userWasChanged = true;
                        service.SetUserAccount(this.Template.UserAccount, this.Template.Password);
                    }

                    if (this.Template.Dependencies != null && !this.Template.Dependencies.ToHashSet().SetEquals(service.Dependencies))
                    {
                        service.SetDependencies(this.Template.Dependencies.ToList());
                    }

                    if (FailureActionsChanged(service))
                    {
                        var timeDelay = this.Template.RestartDelay != null ? TimeSpan.FromMinutes(this.Template.RestartDelay.Value) : TimeSpan.Zero;

                        var newFailureActions = new ServiceFailureActions(
                            resetPeriod: this.Template.RestartDelay,
                            rebootMessage: this.Template.RebootMessage,
                            command: this.Template.OnFailureProgramPath,
                            actions: new[]
                            {
                                new ServiceControllerAction(this.Template.OnFirstFailure ?? ServiceControllerActionType.None, timeDelay),
                                new ServiceControllerAction(this.Template.OnSecondFailure ?? ServiceControllerActionType.None, timeDelay),
                                new ServiceControllerAction(this.Template.OnSubsequentFailures ?? ServiceControllerActionType.None, timeDelay)
                            });

                        service.FailureActions = newFailureActions;
                    }

                    if (this.Template.Status != null)
                    {
                        if (!IsPending(this.Template.Status.Value))
                        {
                            await this.EnsureServiceStatusAsync(service.Name, this.Template.Status.Value);
                        }
                        else
                        {
                            this.LogWarning($"Specified service status \"{this.Template.Status.Value}\" is invalid, therefore the service's status will not be modified.");
                        }
                    }
                }
                else
                {
                    if (service == null)
                    {
                        this.LogWarning("Service doesn't exist.");
                        return;
                    }

                    this.LogDebug("Service exists. Stopping before deleting...");
                    await this.EnsureServiceStatusAsync(service.Name, ServiceControllerStatus.Stopped);

                    this.LogDebug($"Deleting {service.Name} service...");
                    service.Delete();
                }
            }

            if (userWasChanged && this.Template.Status == ServiceControllerStatus.Running)
            {
                this.LogDebug("The service user was changed, therefore the service will be restarted.");
                await this.EnsureServiceStatusAsync(this.Template.Name, ServiceControllerStatus.Stopped);
                await this.EnsureServiceStatusAsync(this.Template.Name, ServiceControllerStatus.Running);
            }
        }

        private bool FailureActionsChanged(WindowsService service)
        {
            if (this.Template.OnFirstFailure != null && this.Template.OnFirstFailure != service.FailureActions.Actions.Cast<ServiceControllerAction?>().FirstOrDefault()?.Type)
                return true;
            if (this.Template.OnSecondFailure != null && this.Template.OnSecondFailure != service.FailureActions.Actions.Cast<ServiceControllerAction?>().Skip(1).FirstOrDefault()?.Type)
                return true;
            if (this.Template.OnSubsequentFailures != null && this.Template.OnSubsequentFailures != service.FailureActions.Actions.Cast<ServiceControllerAction?>().Skip(2).FirstOrDefault()?.Type)
                return true;
            if (this.Template.OnFailureProgramPath != null && this.Template.OnFailureProgramPath != service.FailureActions.Command)
                return true;
            if (this.Template.RebootMessage != null && this.Template.RebootMessage != service.FailureActions.RebootMessage)
                return true;
            if (this.Template.RestartDelay != null && this.Template.RestartDelay != service.FailureActions.ResetPeriod)
                return true;

            return false;
        }

        private async Task EnsureServiceStatusAsync(string serviceName, ServiceControllerStatus desiredStatus)
        {
            var timeout = this.Template?.StatusChangeTimeout ?? TimeSpan.FromSeconds(30);

            using (var controller = new ServiceController(serviceName))
            {
                this.LogDebug($"{controller.ServiceName} service is in {controller.Status} state.");

                if (IsPending(controller.Status))
                {
                    var goalState = GetPendingGoalState(controller.Status);
                    this.LogDebug($"Waiting for state {goalState}...");
                    controller.WaitForStatus(goalState, timeout);
                    this.LogDebug($"Service is in {goalState} state.");
                }

                if (desiredStatus == ServiceControllerStatus.Running && controller.Status != ServiceControllerStatus.Running)
                {
                    this.LogDebug($"Starting the {controller.ServiceName} service...");
                    controller.Start();
                    await Task.Delay(1000);
                    controller.WaitForStatus(ServiceControllerStatus.Running, timeout);
                    this.LogDebug($"{controller.ServiceName} service is now running.");
                }
                else if (desiredStatus == ServiceControllerStatus.Stopped && controller.Status != ServiceControllerStatus.Stopped)
                {
                    if (controller.CanStop)
                    {
                        this.LogDebug($"Stopping the {controller.ServiceName} service...");
                        controller.Stop();
                        await Task.Delay(1000);
                        controller.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                        this.LogDebug($"{controller.ServiceName} service is now stopped.");
                    }
                    else
                    {
                        this.LogWarning($"Service controller reported that {controller.ServiceName} cannot be stopped.");
                    }
                }
                else if (desiredStatus == ServiceControllerStatus.Paused && controller.Status != ServiceControllerStatus.Paused)
                {
                    if (controller.CanPauseAndContinue)
                    {
                        this.LogDebug($"Pausing the {controller.ServiceName} service...");
                        controller.Pause();
                        await Task.Delay(1000);
                        controller.WaitForStatus(ServiceControllerStatus.Paused, timeout);
                        this.LogDebug($"{controller.ServiceName} service is now paused.");
                    }
                    else
                    {
                        this.LogWarning($"Service controller reported that {controller.ServiceName} cannot be paused or continued.");
                    }
                }

                controller.Refresh();

                this.LogDebug($"{controller.ServiceName} service is now in {controller.Status} state.");
            }
        }

        private static bool IsPending(ServiceControllerStatus value)
        {
            return value == ServiceControllerStatus.ContinuePending
                || value == ServiceControllerStatus.PausePending
                || value == ServiceControllerStatus.StartPending
                || value == ServiceControllerStatus.StopPending;
        }

        private static ServiceControllerStatus GetPendingGoalState(ServiceControllerStatus pendingStatus)
        {
            switch (pendingStatus)
            {
                case ServiceControllerStatus.ContinuePending:
                case ServiceControllerStatus.StartPending:
                    return ServiceControllerStatus.Running;

                case ServiceControllerStatus.StopPending:
                    return ServiceControllerStatus.Stopped;

                case ServiceControllerStatus.PausePending:
                    return ServiceControllerStatus.Paused;

                default:
                    return pendingStatus;
            }

        }

        private WindowsService GetOrCreateService(bool allowCreate)
        {
            var service = WindowsService.GetService(this.Template.Name);
            if (service != null)
                return service;

            if (this.Template.Exists)
            {
                this.LogDebug("Does not exist. Creating...");
                if (allowCreate)
                    return WindowsService.CreateService(this.Template.Name, this.Template.DisplayName, this.Template.Path);
            }

            return null;
        }
    }
}
