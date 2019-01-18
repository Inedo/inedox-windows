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
                        $"App pool {this.AppPoolName} does not exist."
                    );

                    return null;
                }

                switch (this.OperationType)
                {
                    case AppPoolOperationType.Start:
                        await this.StartAppPoolAsync(pool, cancellationToken);
                        break;
                    case AppPoolOperationType.Stop:
                        await this.StopAppPoolAsync(pool, cancellationToken);
                        break;
                    case AppPoolOperationType.Recycle:
                        await this.RecycleAppPoolAsync(pool, cancellationToken);
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
            writer.Write(this.WaitForTargetStatus);
        }
        public override void Deserialize(Stream stream)
        {
            var reader = new BinaryReader(stream, InedoLib.UTF8Encoding);
            this.OperationType = (AppPoolOperationType)reader.ReadByte();
            this.AppPoolName = reader.ReadString();
            this.WaitForTargetStatus = reader.ReadBoolean();
        }

        public override void SerializeResponse(Stream stream, object result)
        {
        }
        public override object DeserializeResponse(Stream stream)
        {
            return null;
        }

        private async Task StartAppPoolAsync(ApplicationPool pool, CancellationToken cancellationToken)
        {
            var state = pool.State;
            if (state == ObjectState.Stopped)
            {
                this.LogInformation($"Starting app pool {pool.Name}...");
                var result = pool.Start();
                this.LogInformation($"App pool {pool.Name} state is now {result}.");

                if (this.WaitForTargetStatus)
                {
                    while ((state = pool.State) != ObjectState.Started && state != ObjectState.Stopped)
                    {
                        await Task.Delay(100, cancellationToken);
                    }

                    if (state == ObjectState.Started)
                        this.LogInformation("App pool is started.");
                    else
                        this.LogError("App pool could not be started.");
                }
            }
            else if (state == ObjectState.Started)
            {
                this.LogInformation($"App pool {pool.Name} is already Started.");
            }
            else
            {
                this.LogError($"Cannot start app pool {pool.Name}; current state is {state} (must be Stopped).");
            }
        }
        private async Task StopAppPoolAsync(ApplicationPool pool, CancellationToken cancellationToken)
        {
            var state = pool.State;
            if (state == ObjectState.Started)
            {
                this.LogInformation($"Stopping app pool {pool.Name}...");
                var result = pool.Stop();
                this.LogInformation($"App pool {pool.Name} state is now {result}.");

                if (this.WaitForTargetStatus)
                {
                    while ((state = pool.State) != ObjectState.Stopped)
                    {
                        await Task.Delay(100, cancellationToken);
                    }

                    this.LogInformation("App pool is stopped.");
                }
            }
            else if (state == ObjectState.Stopped)
            {
                this.LogInformation($"App pool {pool.Name} is already Stopped.");
            }
            else
            {
                this.LogError($"Cannot stop app pool {pool.Name}; current state is {state} (must be Started).");
            }
        }
        private async Task RecycleAppPoolAsync(ApplicationPool pool, CancellationToken cancellationToken)
        {
            this.LogInformation($"Recycling app pool {pool.Name}...");
            var result = pool.Recycle();
            this.LogInformation($"App pool {pool.Name} state is now {result}.");

            if (this.WaitForTargetStatus)
            {
                await Task.Delay(100, cancellationToken);

                while (pool.State != ObjectState.Started)
                {
                    await Task.Delay(100, cancellationToken);
                }

                this.LogInformation("App pool is started.");
            }
        }
    }

    internal enum AppPoolOperationType
    {
        Start,
        Stop,
        Recycle
    }
}
