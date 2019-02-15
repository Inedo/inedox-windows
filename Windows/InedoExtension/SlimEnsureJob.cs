using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using Inedo.Serialization;

namespace Inedo.Extensions.Windows
{
    internal abstract class SlimEnsureJob<TTemplate> : RemoteJob
        where TTemplate : PersistedConfiguration, new()
    {
        protected SlimEnsureJob()
        {
        }

        public TTemplate Template { get; set; }
        public bool Ensure { get; set; }
        public bool Simulation { get; set; }

        public static async Task<PersistedConfiguration> CollectAsync<TJob>(EnsureOperation<TTemplate> operation, IOperationCollectionContext context)
            where TJob : SlimEnsureJob<TTemplate>, new()
        {
            var job = new TJob
            {
                Template = operation.Template,
                Ensure = false,
                Simulation = context.Simulation
            };

            job.MessageLogged += (s, e) => operation.Log(e);

            var jobExecuter = await context.Agent.GetServiceAsync<IRemoteJobExecuter>();
            return (PersistedConfiguration)await jobExecuter.ExecuteJobAsync(job, context.CancellationToken);
        }
        public static async Task EnsureAsync<TJob>(EnsureOperation<TTemplate> operation, IOperationExecutionContext context)
            where TJob : SlimEnsureJob<TTemplate>, new()
        {
            var job = new TJob
            {
                Template = operation.Template,
                Ensure = true,
                Simulation = context.Simulation
            };

            job.MessageLogged += (s, e) => operation.Log(e);

            var jobExecuter = await context.Agent.GetServiceAsync<IRemoteJobExecuter>();
            await jobExecuter.ExecuteJobAsync(job, context.CancellationToken);
        }

        public abstract Task<TTemplate> CollectAsync(CancellationToken cancellationToken);
        public abstract Task ConfigureAsync(CancellationToken cancellationToken);

        public override async Task<object> ExecuteAsync(CancellationToken cancellationToken)
        {
            if (this.Ensure)
            {
                await this.ConfigureAsync(cancellationToken);
                return null;
            }
            else
            {
                return await this.CollectAsync(cancellationToken);
            }
        }

        public override void Serialize(Stream stream)
        {
            using (var writer = new BinaryWriter(stream, InedoLib.UTF8Encoding, true))
            {
                writer.Write(this.Ensure);
                writer.Write(this.Simulation);
            }

            SlimBinaryFormatter.Serialize(this.Template, stream);
        }
        public override void Deserialize(Stream stream)
        {
            using (var reader = new BinaryReader(stream, InedoLib.UTF8Encoding, true))
            {
                this.Ensure = reader.ReadBoolean();
                this.Simulation = reader.ReadBoolean();
            }

            this.Template = (TTemplate)SlimBinaryFormatter.Deserialize(stream);
        }

        public override void SerializeResponse(Stream stream, object result)
        {
            if (!this.Ensure)
                SlimBinaryFormatter.Serialize(result, stream);
        }
        public override object DeserializeResponse(Stream stream)
        {
            if (!this.Ensure)
                return SlimBinaryFormatter.Deserialize(stream);
            else
                return null;
        }

        protected ILogSink GetLogWrapper() => new LogSink(this);

        private sealed class LogSink : ILogSink
        {
            private readonly ILogger logger;

            public LogSink(ILogger logger) => this.logger = logger;

            public void Log(IMessage message) => this.logger.Log(message.Level, message.Message);
        }
    }
}
