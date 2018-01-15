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
        internal const string CollectOutputAsDictionary = "{3BB97EBB-FCF5-4BBC-B71C-60DEB4D1BA84}";

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
                            if (this.OutVariables.Contains(CollectOutputAsDictionary))
                            {
                                this.Variables[CollectOutputAsDictionary] = e.Output.Properties
                                    .Where(p => p.IsGettable && p.IsInstance)
                                    .ToDictionary(p => p.Name, p => p.Value?.ToString());
                            }
                            else
                            {
                                var output = e.Output?.ToString();
                                if (!string.IsNullOrWhiteSpace(output))
                                {
                                    lock (outputData)
                                    {
                                        outputData.Add(output);
                                    }
                                }
                            }
                        };
                }

                runner.ProgressUpdate += (s, e) => this.NotifyProgressUpdate(e.PercentComplete, e.Activity);

                var outVariables = this.OutVariables.ToDictionary(v => v, v => (object)null, StringComparer.OrdinalIgnoreCase);

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
                WriteRuntimeValue(writer, v.Value);
            }
        }

        private static void WriteRuntimeValue(BinaryWriter writer, object value)
        {
            var dict = value as System.Collections.IDictionary;
            if (dict != null)
            {
                writer.Write((byte)'%');
                SlimBinaryFormatter.WriteLength(writer, dict.Count);
                foreach (var key in dict.Keys)
                {
                    writer.Write(key?.ToString() ?? string.Empty);
                    WriteRuntimeValue(writer, dict[key]);
                }
            }
            else if (value is string)
            {
                writer.Write((byte)'$');
                writer.Write(value?.ToString() ?? string.Empty);
            }
            else if (value is System.Collections.IEnumerable)
            {
                var list = ((System.Collections.IEnumerable)value).Cast<object>().ToList();
                writer.Write((byte)'@');
                SlimBinaryFormatter.WriteLength(writer, list.Count);
                foreach (var element in list)
                {
                    WriteRuntimeValue(writer, element);
                }
            }
            else
            {
                writer.Write((byte)'$');
                writer.Write(value?.ToString() ?? string.Empty);
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
            var vars = new Dictionary<string, object>(count, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < count; i++)
            {
                var key = reader.ReadString();
                var value = ReadRuntimeValue(reader);
                vars[key] = value;
            }

            return new Result
            {
                ExitCode = exitCode,
                Output = output,
                OutVariables = vars
            };
        }

        private static object ReadRuntimeValue(BinaryReader reader)
        {
            byte type = reader.ReadByte();
            switch (type)
            {
                case (byte)'%':
                    {
                        var count = SlimBinaryFormatter.ReadLength(reader);
                        var dict = new Dictionary<string, object>(count, StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < count; i++)
                        {
                            var key = reader.ReadString();
                            var value = ReadRuntimeValue(reader);
                            dict[key] = value;
                        }
                        return dict;
                    }
                case (byte)'@':
                    {
                        var count = SlimBinaryFormatter.ReadLength(reader);
                        var list = new List<object>(count);
                        for (int i = 0; i < count; i++)
                        {
                            list.Add(ReadRuntimeValue(reader));
                        }
                        return list;
                    }
                case (byte)'$':
                    return reader.ReadString();
                default:
                    throw new InvalidDataException($"Invalid runtime variable type specifier '{type}'");
            }
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
            public Dictionary<string, object> OutVariables { get; set; }
        }
    }
}
