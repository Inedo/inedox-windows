using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Serialization;

namespace Inedo.Extensions.Windows.PowerShell
{
    internal sealed class CollectDscModulesJob : RemoteJob
    {
        public bool DebugLogging { get; set; }
        public bool VerboseLogging { get; set; }
        public bool LogOutput { get; set; }

        public override void Serialize(Stream stream)
        {
            var writer = new BinaryWriter(stream, InedoLib.UTF8Encoding);
            writer.Write(this.DebugLogging);
            writer.Write(this.VerboseLogging);
            writer.Write(this.LogOutput);
        }
        public override void Deserialize(Stream stream)
        {
            var reader = new BinaryReader(stream, InedoLib.UTF8Encoding);
            this.DebugLogging = reader.ReadBoolean();
            this.VerboseLogging = reader.ReadBoolean();
            this.LogOutput = reader.ReadBoolean();
        }

        public override async Task<object> ExecuteAsync(CancellationToken cancellationToken)
        {
            using (var runner = new PowerShellScriptRunner { DebugLogging = this.DebugLogging, VerboseLogging = this.VerboseLogging })
            {
                runner.MessageLogged += (s, e) => this.Log(e.Level, e.Message);
                if (this.LogOutput)
                    runner.OutputReceived += (s, e) => this.LogInformation(e.Output?.ToString());

                var output = new Dictionary<string, object> { { "results", null } };

                int? exitCode = await runner.RunAsync("$results = Get-DscResource", new Dictionary<string, object>(), new Dictionary<string, object>(), output, cancellationToken);

                var infos = ((object[])output["results"]).OfType<PSObject>();

                var modules = from m in infos
                              select ParseModuleInfo(m);

                return new Result
                {
                    ExitCode = exitCode,
                    Modules = modules.ToList()
                };
            }
        }

        private static ModuleInfo ParseModuleInfo(PSObject obj)
        {
            var info = new ModuleInfo
            {
                Name = obj.Properties["Name"].Value.ToString(),
                Version = obj.Properties["Version"]?.Value?.ToString() ?? string.Empty
            };

            return info;
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

            SlimBinaryFormatter.WriteLength(writer, data.Modules.Count);
            foreach (var m in data.Modules)
            {
                writer.Write(m.Name);
                writer.Write(m.Version);
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
            var modules = new List<ModuleInfo>(count);
            for (int i = 0; i < count; i++)
            {
                var info = new ModuleInfo();
                info.Name = reader.ReadString();
                info.Version = reader.ReadString();
                modules.Add(info);
            }

            return new Result
            {
                ExitCode = exitCode,
                Modules = modules
            };
        }

        public sealed class Result
        {
            public int? ExitCode { get; set; }
            public List<ModuleInfo> Modules { get; set; }
        }

        [Serializable]
        public sealed class ModuleInfo
        {
            public string Name { get; set; }
            public string Version { get; set; }
        }
    }
}
