using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.ExecutionEngine;

namespace Inedo.Extensions.Windows.PowerShell
{
    internal sealed class IsolatedPowerShellRunner : MarshalByRefObject, IPowerShellRunner
    {
        public event EventHandler<PowerShellOutputEventArgs> OutputReceived;
        public event EventHandler<LogMessageEventArgs> MessageLogged;
        public event EventHandler<PSProgressEventArgs> ProgressUpdate;

        public bool LogOutput { get; set; }
        public bool CollectOutput { get; set; }
        public bool DebugLogging { get; set; }
        public bool VerboseLogging { get; set; }

        public Task<ExecutePowerShellJob.Result> ExecuteAsync(string script, Dictionary<string, RuntimeValue> variables, Dictionary<string, RuntimeValue> parameters, string[] outVariables, CancellationToken cancellationToken)
        {
            var domainLock = new object();
            var domain = AppDomain.CreateDomain("InedoPSScript", null, new AppDomainSetup { ApplicationBase = Path.GetDirectoryName(typeof(IsolatedPowerShellRunner).Assembly.Location) });
            var registration = new CancellationTokenRegistration();
            try
            {
                var inner = (InnerPowerShellRunner)domain.CreateInstanceAndUnwrap(typeof(IsolatedPowerShellRunner).Assembly.GetName().Name, typeof(InnerPowerShellRunner).FullName);
                inner.ScriptText = script;
                inner.LogOutput = this.LogOutput;
                inner.CollectOutput = this.CollectOutput;
                inner.DebugLogging = this.DebugLogging;
                inner.VerboseLogging = this.VerboseLogging;
                inner.Variables = variables;
                inner.Parameters = parameters;
                inner.OutVariables = outVariables;
                inner.CoreAssemblyPath = Path.GetDirectoryName(typeof(AH).Assembly.Location);

                inner.Initialize();

                inner.OutputReceived = this.HandleOutputReceived;
                inner.MessageLogged = this.HandleMessageLogged;
                inner.ProgressUpdate = this.HandleProgressUpdate;

                registration = cancellationToken.Register(unloadAppDomain);

                return Task.FromResult(inner.Execute());
            }
            catch (AppDomainUnloadedException)
            {
                throw new TaskCanceledException();
            }
            finally
            {
                unloadAppDomain();
                registration.Dispose();
            }

            void unloadAppDomain()
            {
                lock (domainLock)
                {
                    if (domain != null)
                    {
                        AppDomain.Unload(domain);
                        domain = null;
                    }
                }
            }
        }

        // these methods can't be lambda expressions because they have to be MarshalByRef
        private void HandleOutputReceived(string o) => this.OutputReceived?.Invoke(this, new PowerShellOutputEventArgs(new System.Management.Automation.PSObject(o)));
        private void HandleMessageLogged(MessageLevel l, string m) => this.MessageLogged?.Invoke(this, new LogMessageEventArgs(l, m));
        private void HandleProgressUpdate(int p, string a) => this.ProgressUpdate?.Invoke(this, new PSProgressEventArgs(p, a));

        private sealed class InnerPowerShellRunner : MarshalByRefObject
        {
            public string ScriptText { get; set; }
            public bool LogOutput { get; set; }
            public bool CollectOutput { get; set; }
            public bool DebugLogging { get; set; }
            public bool VerboseLogging { get; set; }
            public Dictionary<string, RuntimeValue> Variables { get; set; }
            public Dictionary<string, RuntimeValue> Parameters { get; set; }
            public string[] OutVariables { get; set; }
            public Action<string> OutputReceived { get; set; }
            public Action<MessageLevel, string> MessageLogged { get; set; }
            public Action<int, string> ProgressUpdate { get; set; }
            public string CoreAssemblyPath { get; set; }

            public void Initialize() => AppDomain.CurrentDomain.AssemblyResolve += this.CurrentDomain_AssemblyResolve;

            public ExecutePowerShellJob.Result Execute()
            {
                using (var runner = new PowerShellScriptRunner { DebugLogging = this.DebugLogging, VerboseLogging = this.VerboseLogging })
                {
                    var outputData = new List<RuntimeValue>();

                    runner.MessageLogged += (s, e) => this.MessageLogged(e.Level, e.Message);
                    if (this.LogOutput)
                        runner.OutputReceived += (s, e) => this.MessageLogged(MessageLevel.Information, e.Output?.ToString());

                    var outVariables = this.OutVariables.ToDictionary(v => v, v => new RuntimeValue(string.Empty), StringComparer.OrdinalIgnoreCase);

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

                    runner.ProgressUpdate += (s, e) => this.ProgressUpdate(e.PercentComplete, e.Activity);

                    int? exitCode = runner.RunAsync(this.ScriptText, this.Variables, this.Parameters, outVariables, default).GetAwaiter().GetResult();

                    return new ExecutePowerShellJob.Result
                    {
                        ExitCode = exitCode,
                        Output = outputData,
                        OutVariables = outVariables
                    };
                }
            }

            private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
            {
                // this isn't a great way to do it, but will have to do until there is proper SDK support for creating an AppDomain
                var name = new AssemblyName(args.Name).Name;
                var fileName = Path.Combine(this.CoreAssemblyPath, name + ".dll");
                if (File.Exists(fileName))
                    return Assembly.LoadFrom(fileName);

                return null;
            }
        }
    }
}
