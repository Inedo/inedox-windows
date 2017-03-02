using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Serialization;

namespace Inedo.Extensions.Windows.PowerShell
{
    internal sealed class ExecutePowerShellDscJob : RemoteJob
    {
        public string ScriptText { get; set; }

        public override void Serialize(Stream stream)
        {
            var writer = new BinaryWriter(stream, InedoLib.UTF8Encoding);
            writer.Write(this.ScriptText ?? string.Empty);
        }
        public override void Deserialize(Stream stream)
        {
            var reader = new BinaryReader(stream, InedoLib.UTF8Encoding);
            this.ScriptText = reader.ReadString();
        }

        public override async Task<object> ExecuteAsync(CancellationToken cancellationToken)
        {
            using (var runner = new PowerShellScriptRunner { DebugLogging = true })
            {
                var outputData = new Dictionary<string, string>();

                runner.MessageLogged += (s, e) => this.Log(e.Level, e.Message);

                runner.OutputReceived +=
                    (s, e) =>
                    {
                        if (e.Output != null)
                        {
                            lock (outputData)
                            {
                                var properties = from p in e.Output.Properties
                                                 where p.IsGettable && p.IsInstance
                                                 select p;

                                foreach (var property in properties)
                                    outputData[property.Name] = property.Value?.ToString();
                            }
                        }
                    };

                int? exitCode = await runner.RunAsync(this.ScriptText, new Dictionary<string, object>(), new Dictionary<string, object>(), cancellationToken);

                return new Result
                {
                    ExitCode = exitCode,
                    Output = outputData
                };
            }
        }

        public override void SerializeResponse(Stream stream, object result)
        {
            var data = (Result)result;
            var writer = new BinaryWriter(stream, InedoLib.UTF8Encoding);

            if (data.ExitCode == null)
            {
                stream.WriteByte(0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(data.ExitCode.Value);
            }

            SlimBinaryFormatter.WriteLength(stream, data.Output.Count);
            foreach (var s in data.Output)
            {
                writer.Write(s.Key ?? string.Empty);
                writer.Write(s.Value ?? string.Empty);
            }
        }
        public override object DeserializeResponse(Stream stream)
        {
            var reader = new BinaryReader(stream, InedoLib.UTF8Encoding);

            int? exitCode;
            if (stream.ReadByte() == 0)
                exitCode = null;
            else
                exitCode = reader.ReadInt32();

            int count = SlimBinaryFormatter.ReadLength(stream);
            var output = new Dictionary<string, string>(count);
            for (int i = 0; i < count; i++)
            {
                var key = reader.ReadString();
                var value = reader.ReadString();
                output[key] = value;
            }

            return new Result
            {
                ExitCode = exitCode,
                Output = output
            };
        }

        public sealed class Result
        {
            public int? ExitCode { get; set; }
            public Dictionary<string, string> Output { get; set; }
        }
    }
}
