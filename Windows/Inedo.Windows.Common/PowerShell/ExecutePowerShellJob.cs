using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Serialization;

namespace Inedo.Extensions.Windows.PowerShell
{
    internal sealed class ExecutePowerShellJob : RemoteJob
    {
        private int currentPercent;
        private string currentActivity = string.Empty;

        public event EventHandler<PSProgressEventArgs> ProgressUpdate;

        public string ScriptText { get; set; }
        public bool DebugLogging { get; set; }
        public bool VerboseLogging { get; set; }
        public bool CollectOutput { get; set; }
        public bool LogOutput { get; set; }
        public Dictionary<string, object> Variables { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public string[] OutVariables { get; set; }

        public override void Serialize(Stream stream)
        {
            var writer = new BinaryWriter(stream, InedoLib.UTF8Encoding);
            writer.Write(this.ScriptText ?? string.Empty);
            writer.Write(this.DebugLogging);
            writer.Write(this.VerboseLogging);
            writer.Write(this.CollectOutput);
            writer.Write(this.LogOutput);

            SlimBinaryFormatter.Serialize(this.Variables, stream);
            SlimBinaryFormatter.Serialize(this.Parameters, stream);

            SlimBinaryFormatter.WriteLength(stream, this.OutVariables?.Length ?? 0);
            foreach (var var in this.OutVariables ?? new string[0])
                writer.Write(var);
        }
        public override void Deserialize(Stream stream)
        {
            var reader = new BinaryReader(stream, InedoLib.UTF8Encoding);
            this.ScriptText = reader.ReadString();
            this.DebugLogging = reader.ReadBoolean();
            this.VerboseLogging = reader.ReadBoolean();
            this.CollectOutput = reader.ReadBoolean();
            this.LogOutput = reader.ReadBoolean();

            this.Variables = (Dictionary<string, object>)SlimBinaryFormatter.Deserialize(stream) ?? new Dictionary<string, object>();
            this.Parameters = (Dictionary<string, object>)SlimBinaryFormatter.Deserialize(stream) ?? new Dictionary<string, object>();

            int count = SlimBinaryFormatter.ReadLength(stream);
            this.OutVariables = new string[count];
            for (int i = 0; i < count; i++)
                this.OutVariables[i] = reader.ReadString();
        }

        public override async Task<object> ExecuteAsync(CancellationToken cancellationToken)
        {
            using (var runner = new PowerShellScriptRunner { DebugLogging = this.DebugLogging, VerboseLogging = this.VerboseLogging })
            {
                var outputData = new List<string>();

                runner.MessageLogged += (s, e) => this.Log(e.Level, e.Message);
                if (this.LogOutput)
                    runner.OutputReceived += (s, e) => this.LogInformation(e.Output?.ToString());

                if (this.CollectOutput)
                {
                    runner.OutputReceived +=
                        (s, e) =>
                        {
                            var output = e.Output?.ToString();
                            if (!string.IsNullOrWhiteSpace(output))
                            {
                                lock (outputData)
                                {
                                    outputData.Add(output);
                                }
                            }
                        };
                }

                runner.ProgressUpdate += (s, e) => this.NotifyProgressUpdate(e.PercentComplete, e.Activity);

                var outVariables = this.OutVariables.ToDictionary(v => v, v => (string)null, StringComparer.OrdinalIgnoreCase);

                int? exitCode = await runner.RunAsync(this.ScriptText, this.Variables, this.Parameters, outVariables, cancellationToken);

                return new Result
                {
                    ExitCode = exitCode,
                    Output = outputData,
                    OutVariables = outVariables
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
                writer.Write(s ?? string.Empty);

            SlimBinaryFormatter.WriteLength(stream, data.OutVariables.Count);
            foreach (var v in data.OutVariables)
            {
                writer.Write(v.Key ?? string.Empty);
                writer.Write(v.Value ?? string.Empty);
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
            var output = new List<string>(count);
            for (int i = 0; i < count; i++)
                output.Add(reader.ReadString());

            count = SlimBinaryFormatter.ReadLength(stream);
            var vars = new Dictionary<string, string>(count, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < count; i++)
            {
                var key = reader.ReadString();
                var value = reader.ReadString();
                vars[key] = value;
            }

            return new Result
            {
                ExitCode = exitCode,
                Output = output,
                OutVariables = vars
            };
        }

        protected override void DataReceived(byte[] data)
        {
            int percent = data[0];
            var activity = InedoLib.UTF8Encoding.GetString(data, 1, data.Length - 1);
            this.ProgressUpdate?.Invoke(this, new PSProgressEventArgs(percent, activity));
        }

        private void NotifyProgressUpdate(int percent, string activity)
        {
            if (percent != this.currentPercent || activity != this.currentActivity)
            {
                this.currentPercent = percent;
                this.currentActivity = activity;

                var buffer = new byte[InedoLib.UTF8Encoding.GetByteCount(activity) + 1];
                buffer[0] = (byte)percent;
                InedoLib.UTF8Encoding.GetBytes(activity, 0, activity.Length, buffer, 1);
                this.Post(buffer);
            }
        }

        public sealed class Result
        {
            public int? ExitCode { get; set; }
            public List<string> Output { get; set; }
            public Dictionary<string, string> OutVariables { get; set; }
        }
    }
}
