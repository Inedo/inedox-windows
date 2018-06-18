using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.ExecutionEngine;
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

                var output = new Dictionary<string, RuntimeValue>
                {
                    ["results"] = default
                };

                int? exitCode = await runner.RunAsync("$results = Get-DscResource", outVariables: output, cancellationToken: cancellationToken);

                var infos = output["results"].AsEnumerable() ?? Enumerable.Empty<RuntimeValue>();

                return new Result
                {
                    ExitCode = exitCode,
                    Modules = infos.Select(parseModuleInfo).ToList()
                };
            }

            ModuleInfo parseModuleInfo(RuntimeValue value)
            {
                var d = value.AsDictionary();
                if (d == null)
                    return null;

                if (!d.TryGetValue("Name", out var name) || string.IsNullOrWhiteSpace(name.AsString()))
                    return null;

                d.TryGetValue("Version", out var version);

                return new ModuleInfo
                {
                    Name = name.AsString(),
                    Version = version.AsString() ?? string.Empty
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
