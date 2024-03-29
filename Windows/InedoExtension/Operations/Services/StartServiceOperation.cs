﻿using System;
using System.ComponentModel;
using System.ServiceProcess;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Windows.Operations.Services
{
    [DisplayName("Start Windows Service")]
    [Description("Starts an existing Windows service.")]
    [DefaultProperty(nameof(ServiceName))]
    [ScriptAlias("Start-Service")]
    [Tag(Tags.Services)]
    [Example(@"# starts the FOOBAR service on the remote server
Start-Service FOOBAR;")]
    [ScriptNamespace(Namespaces.Windows)]
    public sealed class StartServiceOperation : ExecuteOperation
    {
        [Required]
        [ScriptAlias("Name")]
        [DisplayName("Service name")]
        public string ServiceName { get; set; }
        [ScriptAlias("WaitForRunningStatus")]
        [DisplayName("Wait for running status")]
        [DefaultValue(true)]
        public bool WaitForRunningStatus { get; set; }
        [DefaultValue(false)]
        [ScriptAlias("FailIfServiceDoesNotExist")]
        [DisplayName("Fail if service does not exist")]
        public bool FailIfServiceDoesNotExist { get; set; }

        public override Task ExecuteAsync(IOperationExecutionContext context)
        {
            if (!OperatingSystem.IsWindows())
                throw new ExecutionFailureException("This operation requires Windows.");

            this.LogInformation($"Starting service {this.ServiceName}...");
            if (context.Simulation)
            {
                this.LogInformation("Service is running.");
                return Complete;
            }

            var jobExecuter = context.Agent.GetService<IRemoteJobExecuter>();
            var job = new ControlServiceJob 
            {
                ServiceName = this.ServiceName, 
                TargetStatus = ServiceControllerStatus.Running, 
                WaitForTargetStatus = this.WaitForRunningStatus,
                FailIfServiceDoesNotExist = this.FailIfServiceDoesNotExist
            };
            job.MessageLogged += (s, e) => this.Log(e.Level, e.Message);
            return jobExecuter.ExecuteJobAsync(job, context.CancellationToken);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Start ",
                    new Hilite(config[nameof(ServiceName)]),
                    " service"
                )
            );
        }
    }
}
