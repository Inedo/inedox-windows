using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Microsoft.Web.Administration;

namespace Inedo.Extensions.Windows.Operations.IIS.AppPools
{
    internal sealed class AppPoolJob : RemoteJob
    {
        public AppPoolOperationType OperationType { get; set; }
        public string AppPoolName { get; set; }
        public bool WaitForTargetStatus { get; set; }

        public override async Task<object> ExecuteAsync(CancellationToken cancellationToken)
        {
            using (var server = new ServerManager())
            {
                var pool = server.ApplicationPools[this.AppPoolName];
                if (pool == null)
                {
                    this.Log(
                        this.OperationType == AppPoolOperationType.Stop ? MessageLevel.Warning : MessageLevel.Error,
                        $"Application pool {this.AppPoolName} does not exist."
                    );

                    return null;
                }

                switch (this.OperationType)
                {
                    case AppPoolOperationType.Start:
                        this.StartAppPool(pool);
                        break;
                    case AppPoolOperationType.Stop:
                        await this.StopAppPoolAsync(pool, cancellationToken);
                        break;
                    case AppPoolOperationType.Recycle:
                        this.RecycleAppPool(pool);
                        break;
                }
            }

            return null;
        }

        public override void Serialize(Stream stream)
        {
            var writer = new BinaryWriter(stream, InedoLib.UTF8Encoding);
            writer.Write((byte)this.OperationType);
            writer.Write(this.AppPoolName ?? string.Empty);
        }
        public override void Deserialize(Stream stream)
        {
            var reader = new BinaryReader(stream, InedoLib.UTF8Encoding);
            this.OperationType = (AppPoolOperationType)reader.ReadByte();
            this.AppPoolName = reader.ReadString();
        }

        public override void SerializeResponse(Stream stream, object result)
        {
        }
        public override object DeserializeResponse(Stream stream)
        {
            return null;
        }

        private void StartAppPool(ApplicationPool pool)
        {
            var state = pool.State;
            if (state == ObjectState.Stopped)
            {
                this.LogInformation($"Starting application pool {pool.Name}...");
                var result = pool.Start();
                this.LogInformation($"Application pool {pool.Name} state is now {result}.");
            }
            else if (state == ObjectState.Started)
            {
                this.LogInformation($"Application pool {pool.Name} is already Started.");
            }
            else
            {
                this.LogError($"Cannot start application pool {pool.Name}; current state is {state} (must be Stopped).");
            }
        }
        private async Task StopAppPoolAsync(ApplicationPool pool, CancellationToken cancellationToken)
        {
            var state = pool.State;
            if (state == ObjectState.Started)
            {
                this.LogInformation($"Stopping application pool {pool.Name}...");
                var result = pool.Stop();
                this.LogInformation($"Application pool {pool.Name} state is now {result}.");

                if (this.WaitForTargetStatus)
                {
                    while ((state = pool.State) != ObjectState.Stopped)
                    {
                        await Task.Delay(100, cancellationToken);
                    }

                    this.LogInformation("Application pool is stopped.");
                }
            }
            else if (state == ObjectState.Stopped)
            {
                this.LogInformation($"Application pool {pool.Name} is already Stopped.");
            }
            else
            {
                this.LogError($"Cannot stop application pool {pool.Name}; current state is {state} (must be Started).");
            }
        }
        private void RecycleAppPool(ApplicationPool pool)
        {
            this.LogInformation($"Recycling application pool {pool.Name}...");
            var result = pool.Recycle();
            this.LogInformation($"Application pool {pool.Name} state is now {result}.");
        }
    }

    internal enum AppPoolOperationType
    {
        Start,
        Stop,
        Recycle
    }
}
