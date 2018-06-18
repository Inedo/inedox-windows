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
        public Dictionary<string, RuntimeValue> Variables { get; set; }
        public Dictionary<string, RuntimeValue> Parameters { get; set; }
        public string[] OutVariables { get; set; }
        public bool Isolated { get; set; }

        public override async Task<object> ExecuteAsync(CancellationToken cancellationToken)
        {
            var runner = this.CreateRunner();
            runner.MessageLogged += (s, e) => this.Log(e.Level, e.Message);
            if (this.LogOutput)
                runner.OutputReceived += (s, e) => this.LogInformation(e.Output?.ToString());

            return await runner.ExecuteAsync(this.ScriptText, this.Variables, this.Parameters, this.OutVariables, cancellationToken);
        }

        public override void Serialize(Stream stream)
        {
            var writer = new BinaryWriter(stream, InedoLib.UTF8Encoding);
            writer.Write(this.ScriptText ?? string.Empty);
            writer.Write(this.DebugLogging);
            writer.Write(this.VerboseLogging);
            writer.Write(this.CollectOutput);
            writer.Write(this.LogOutput);
            writer.Write(this.Isolated);

            WriteDictionary(writer, this.Variables);
            WriteDictionary(writer, this.Parameters);

            SlimBinaryFormatter.WriteLength(writer, this.OutVariables?.Length ?? 0);
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
            this.Isolated = reader.ReadBoolean();

            this.Variables = ReadDictionary(reader);
            this.Parameters = ReadDictionary(reader);

            int count = SlimBinaryFormatter.ReadLength(stream);
            this.OutVariables = new string[count];
            for (int i = 0; i < count; i++)
                this.OutVariables[i] = reader.ReadString();
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
                WriteRuntimeValue(writer, s);

            SlimBinaryFormatter.WriteLength(stream, data.OutVariables.Count);
            foreach (var v in data.OutVariables)
            {
                writer.Write(v.Key ?? string.Empty);
                WriteRuntimeValue(writer, v.Value);
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
            var output = new List<RuntimeValue>(count);
            for (int i = 0; i < count; i++)
                output.Add(ReadRuntimeValue(reader));

            var vars = ReadDictionary(reader);

            return new Result
            {
                ExitCode = exitCode,
                Output = output,
                OutVariables = vars
            };
        }

        private static RuntimeValue ReadRuntimeValue(BinaryReader reader)
        {
            var type = (RuntimeValueType)reader.ReadByte();
            if (type == RuntimeValueType.Scalar)
            {
                return reader.ReadString();
            }
            else if (type == RuntimeValueType.Vector)
            {
                int length = SlimBinaryFormatter.ReadLength(reader);
                var list = new List<RuntimeValue>(length);
                for (int i = 0; i < length; i++)
                    list.Add(ReadRuntimeValue(reader));

                return new RuntimeValue(list);
            }
            else if (type == RuntimeValueType.Map)
            {
                return new RuntimeValue(ReadDictionary(reader));
            }
            else
            {
                throw new InvalidDataException("Unknown value type: " + type);
            }
        }
        private static void WriteRuntimeValue(BinaryWriter writer, RuntimeValue value)
        {
            var type = value.ValueType;

            writer.Write((byte)type);

            if (type == RuntimeValueType.Scalar)
            {
                writer.Write(value.AsString() ?? string.Empty);
            }
            else if (type == RuntimeValueType.Vector)
            {
                var list = value.AsEnumerable().ToList();
                SlimBinaryFormatter.WriteLength(writer, list.Count);
                foreach (var i in list)
                    WriteRuntimeValue(writer, i);
            }
            else if (type == RuntimeValueType.Map)
            {
                WriteDictionary(writer, value.AsDictionary());
            }
            else
            {
                throw new ArgumentException("Unknown value type: " + type);
            }
        }
        private static Dictionary<string, RuntimeValue> ReadDictionary(BinaryReader reader)
        {
            var d = new Dictionary<string, RuntimeValue>(StringComparer.OrdinalIgnoreCase);
            int length = SlimBinaryFormatter.ReadLength(reader);
            for (int i = 0; i < length; i++)
            {
                var key = reader.ReadString();
                var value = ReadRuntimeValue(reader);
                d[key] = value;
            }

            return d;
        }
        private static void WriteDictionary(BinaryWriter writer, IDictionary<string, RuntimeValue> d)
        {
            SlimBinaryFormatter.WriteLength(writer, d?.Count ?? 0);
            if (d != null)
            {
                foreach (var p in d)
                {
                    writer.Write(p.Key);
                    WriteRuntimeValue(writer, p.Value);
                }
            }
        }

        protected override void DataReceived(byte[] data)
        {
            int percent = data[0];
            var activity = InedoLib.UTF8Encoding.GetString(data, 1, data.Length - 1);
            this.ProgressUpdate?.Invoke(this, new PSProgressEventArgs(percent, activity));
        }

        private IPowerShellRunner CreateRunner()
        {
            IPowerShellRunner r;

            if (this.Isolated)
                r = new IsolatedPowerShellRunner();
            else
                r = new StandardRunner();

            r.LogOutput = this.LogOutput;
            r.CollectOutput = this.CollectOutput;
            r.DebugLogging = this.DebugLogging;
            r.VerboseLogging = this.VerboseLogging;

            return r;
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

        [Serializable]
        public sealed class Result
        {
            public int? ExitCode { get; set; }
            public List<RuntimeValue> Output { get; set; }
            public Dictionary<string, RuntimeValue> OutVariables { get; set; }
        }

        private sealed class StandardRunner : IPowerShellRunner
        {
            public bool LogOutput { get; set; }
            public bool CollectOutput { get; set; }
            public bool DebugLogging { get; set; }
            public bool VerboseLogging { get; set; }

            public event EventHandler<PowerShellOutputEventArgs> OutputReceived;
            public event EventHandler<LogMessageEventArgs> MessageLogged;
            public event EventHandler<PSProgressEventArgs> ProgressUpdate;

            public async Task<Result> ExecuteAsync(string script, Dictionary<string, RuntimeValue> variables, Dictionary<string, RuntimeValue> parameters, string[] outVariables, CancellationToken cancellationToken)
            {
                using (var runner = new PowerShellScriptRunner { DebugLogging = this.DebugLogging, VerboseLogging = this.VerboseLogging })
                {
                    var outputData = new List<RuntimeValue>();

                    runner.MessageLogged += (s, e) => this.MessageLogged?.Invoke(this, e);
                    if (this.LogOutput)
                        runner.OutputReceived += (s, e) => this.OutputReceived?.Invoke(this, e);

                    var outVariables2 = outVariables.ToDictionary(v => v, v => new RuntimeValue(string.Empty), StringComparer.OrdinalIgnoreCase);

                    if (this.CollectOutput)
                    {
                        runner.OutputReceived +=
                            (s, e) =>
                            {
                                var output = PSUtil.ToRuntimeValue(e.Output);
                                lock (outputData)
                                {
                                    outputData.Add(output);
                                }
                            };
                    }

                    runner.ProgressUpdate += (s, e) => this.ProgressUpdate?.Invoke(this, e);

                    int? exitCode = await runner.RunAsync(script, variables, parameters, outVariables2, cancellationToken);

                    return new Result
                    {
                        ExitCode = exitCode,
                        Output = outputData,
                        OutVariables = outVariables2
                    };
                }
            }
        }
    }
}
