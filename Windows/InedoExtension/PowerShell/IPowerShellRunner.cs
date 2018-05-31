using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.ExecutionEngine;

namespace Inedo.Extensions.Windows.PowerShell
{
    internal interface IPowerShellRunner
    {
        event EventHandler<PowerShellOutputEventArgs> OutputReceived;
        event EventHandler<LogMessageEventArgs> MessageLogged;
        event EventHandler<PSProgressEventArgs> ProgressUpdate;

        bool LogOutput { get; set; }
        bool CollectOutput { get; set; }
        bool DebugLogging { get; set; }
        bool VerboseLogging { get; set; }

        Task<ExecutePowerShellJob.Result> ExecuteAsync(string script, Dictionary<string, RuntimeValue> variables, Dictionary<string, RuntimeValue> parameters, string[] outVariables, CancellationToken cancellationToken);
    }
}
