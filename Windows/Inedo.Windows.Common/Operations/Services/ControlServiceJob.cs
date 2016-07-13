using System;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;

namespace Inedo.Extensions.Windows.Operations.Services
{
    internal sealed class ControlServiceJob : RemoteJob
    {
        public string ServiceName { get; set; }
        public ServiceControllerStatus TargetStatus { get; set; }
        public bool WaitForTargetStatus { get; set; }

        public override async Task<object> ExecuteAsync(CancellationToken cancellationToken)
        {
            using (var service = new ServiceController(this.ServiceName))
            {
                if (this.TargetStatus == ServiceControllerStatus.Running)
                {
                    if (service.Status == ServiceControllerStatus.Running)
                    {
                        this.LogInformation("Service is already running.");
                        return null;
                    }

                    service.Start();
                    if (this.WaitForTargetStatus)
                        await this.WaitForStartAsync(service, cancellationToken);
                }
                else if (this.TargetStatus == ServiceControllerStatus.Stopped)
                {
                    if (service.Status == ServiceControllerStatus.Stopped)
                    {
                        this.LogInformation("Service is already stopped.");
                        return null;
                    }

                    service.Stop();
                    if (this.WaitForTargetStatus)
                        await this.WaitForStopAsync(service, cancellationToken);
                }
                else
                {
                    throw new InvalidOperationException("Cannot change service status to " + this.TargetStatus);
                }
            }

            return null;
        }

        private async Task WaitForStartAsync(ServiceController service, CancellationToken cancellationToken)
        {
            ServiceControllerStatus status;
            while ((status = service.Status) == ServiceControllerStatus.Stopped)
            {
                service.Refresh();
                await Task.Delay(100, cancellationToken);
            }

            this.LogDebug("Service status is " + status);

            while ((status = service.Status) != ServiceControllerStatus.Running && status != ServiceControllerStatus.Stopped)
            {
                service.Refresh();
                await Task.Delay(100, cancellationToken);
            }

            if (status == ServiceControllerStatus.Running)
                this.LogInformation("Service is running.");
            else
               this.LogError("Service stopped immediately after starting.");
        }
        private async Task WaitForStopAsync(ServiceController service, CancellationToken cancellationToken)
        {
            ServiceControllerStatus status;
            while ((status = service.Status) != ServiceControllerStatus.Running)
            {
                service.Refresh();
                await Task.Delay(100, cancellationToken);
            }

            this.LogDebug("Service status is " + status);

            while ((status = service.Status) != ServiceControllerStatus.Stopped)
            {
                service.Refresh();
                await Task.Delay(100, cancellationToken);
            }

            this.LogInformation("Service is stopped.");
        }

        public override void Serialize(Stream stream)
        {
            var writer = new BinaryWriter(stream, InedoLib.UTF8Encoding);
            writer.Write(this.ServiceName);
            writer.Write((int)this.TargetStatus);
            writer.Write(this.WaitForTargetStatus);
        }
        public override void Deserialize(Stream stream)
        {
            var reader = new BinaryReader(stream, InedoLib.UTF8Encoding);
            this.ServiceName = reader.ReadString();
            this.TargetStatus = (ServiceControllerStatus)reader.ReadInt32();
            this.WaitForTargetStatus = reader.ReadBoolean();
        }

        public override void SerializeResponse(Stream stream, object result)
        {
        }
        public override object DeserializeResponse(Stream stream)
        {
            return null;
        }
    }
}
