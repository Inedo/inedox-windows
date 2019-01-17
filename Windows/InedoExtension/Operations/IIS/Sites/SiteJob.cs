using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Microsoft.Web.Administration;

namespace Inedo.Extensions.Windows.Operations.IIS.Sites
{
    internal sealed class SiteJob : RemoteJob
    {
        public string SiteName { get; set; }
        public SiteOperationType OperationType { get; set; }
        public bool WaitForTargetStatus { get; set; }

        public override async Task<object> ExecuteAsync(CancellationToken cancellationToken)
        {
            using (var server = new ServerManager())
            {
                var pool = server.Sites[this.SiteName];
                if (pool == null)
                {
                    this.Log(
                        this.OperationType == SiteOperationType.Stop ? MessageLevel.Warning : MessageLevel.Error,
                        $"Site {this.SiteName} does not exist."
                    );

                    return null;
                }

                switch (this.OperationType)
                {
                    case SiteOperationType.Start:
                        this.StartSite(pool);
                        break;
                    case SiteOperationType.Stop:
                        await this.StopSiteAsync(pool, cancellationToken);
                        break;
                }
            }

            return null;
        }

        public override void Serialize(Stream stream)
        {
            var writer = new BinaryWriter(stream, InedoLib.UTF8Encoding);
            writer.Write((byte)this.OperationType);
            writer.Write(this.SiteName ?? string.Empty);
            writer.Write(this.WaitForTargetStatus);
        }
        public override void Deserialize(Stream stream)
        {
            var reader = new BinaryReader(stream, InedoLib.UTF8Encoding);
            this.OperationType = (SiteOperationType)reader.ReadByte();
            this.SiteName = reader.ReadString();
            this.WaitForTargetStatus = reader.ReadBoolean();
        }

        public override void SerializeResponse(Stream stream, object result)
        {
        }
        public override object DeserializeResponse(Stream stream)
        {
            return null;
        }

        private void StartSite(Site site)
        {
            var state = site.State;
            if (state == ObjectState.Stopped)
            {
                this.LogInformation($"Starting site {site.Name}...");
                var result = site.Start();
                this.LogInformation($"Site {site.Name} state is now {result}.");
            }
            else if (state == ObjectState.Started)
            {
                this.LogInformation($"Site {site.Name} is already Started.");
            }
            else
            {
                this.LogError($"Cannot start site {site.Name}; current state is {state} (must be Stopped).");
            }
        }
        private async Task StopSiteAsync(Site site, CancellationToken cancellationToken)
        {
            var state = site.State;
            if (state == ObjectState.Started)
            {
                this.LogInformation($"Stopping site {site.Name}...");
                var result = site.Stop();
                this.LogInformation($"Site {site.Name} state is now {result}.");

                if (this.WaitForTargetStatus)
                {
                    while ((state = site.State) != ObjectState.Stopped)
                    {
                        await Task.Delay(100, cancellationToken);
                    }

                    this.LogInformation("Application pool is stopped.");
                }
            }
            else if (state == ObjectState.Stopped)
            {
                this.LogInformation($"Site {site.Name} is already Stopped.");
            }
            else
            {
                this.LogError($"Cannot stop Site {site.Name}; current state is {state} (must be Started).");
            }
        }
    }

    internal enum SiteOperationType
    {
        Start,
        Stop
    }
}
