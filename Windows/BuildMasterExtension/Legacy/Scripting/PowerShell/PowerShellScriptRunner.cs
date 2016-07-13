using System;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Extensibility.Scripting;
using Inedo.Diagnostics;

namespace Inedo.BuildMasterExtensions.Windows.Scripting.PowerShell
{
    internal sealed class PowerShellScriptRunner : ActiveScriptBase
    {
        private BuildMasterPSHost pshost = new BuildMasterPSHost();
        private Runspace runspace;
        private System.Management.Automation.PowerShell powerShell;
        private PSDataCollection<PSObject> outputData = new PSDataCollection<PSObject>();
        private Task runScriptTask;
        private bool disposed;

        public PowerShellScriptRunner()
        {
            this.runspace = RunspaceFactory.CreateRunspace(this.pshost);
            this.runspace.Open();
            this.powerShell = System.Management.Automation.PowerShell.Create();
            this.powerShell.Runspace = this.runspace;

            this.pshost.MessageLogged += (s, e) => this.OnLogReceived(new LogReceivedEventArgs(e.Message, e.Level));

            this.powerShell.Streams.Debug.DataAdded += this.Debug_DataAdded;
            this.powerShell.Streams.Error.DataAdded += this.Error_DataAdded;
            this.powerShell.Streams.Verbose.DataAdded += this.Verbose_DataAdded;
            this.powerShell.Streams.Warning.DataAdded += this.Warning_DataAdded;
            this.outputData.DataAdded += this.Output_DataAdded;
        }

        internal System.Management.Automation.PowerShell PowerShell => this.powerShell;

        public override void Start()
        {
            this.runScriptTask = Task.Factory.FromAsync(
                this.powerShell.BeginInvoke<object, PSObject>(null, this.outputData),
                a => { this.powerShell.EndInvoke(a); }
            ).ContinueWith(this.HandleScriptCompleted);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !this.disposed)
            {
                this.powerShell.Streams.Debug.DataAdded -= this.Debug_DataAdded;
                this.powerShell.Streams.Error.DataAdded -= this.Error_DataAdded;
                this.powerShell.Streams.Verbose.DataAdded -= this.Verbose_DataAdded;
                this.powerShell.Streams.Warning.DataAdded -= this.Warning_DataAdded;
                this.outputData.DataAdded -= this.Output_DataAdded;
                this.powerShell.Dispose();
                this.runspace.Dispose();
                this.disposed = true;
            }

            base.Dispose(disposing);
        }

        private void HandleScriptCompleted(Task task)
        {
            var exception = task.Exception;
            if (exception == null)
            {
                this.ScriptCompleted(true, null);
                return;
            }

            this.OnLogReceived(new LogReceivedEventArgs(exception.InnerException.ToString(), MessageLevel.Error));
            this.ScriptCompleted(false, null);
        }
        private void Debug_DataAdded(object sender, DataAddedEventArgs e)
        {
            this.LogMessage((PSDataCollection<DebugRecord>)sender, e.Index, MessageLevel.Debug);
        }
        private void Error_DataAdded(object sender, DataAddedEventArgs e)
        {
            this.LogMessage((PSDataCollection<ErrorRecord>)sender, e.Index, MessageLevel.Error);
        }
        private void Verbose_DataAdded(object sender, DataAddedEventArgs e)
        {
            this.LogMessage((PSDataCollection<VerboseRecord>)sender, e.Index, MessageLevel.Debug);
        }
        private void Warning_DataAdded(object sender, DataAddedEventArgs e)
        {
            this.LogMessage((PSDataCollection<WarningRecord>)sender, e.Index, MessageLevel.Warning);
        }
        private void Output_DataAdded(object sender, DataAddedEventArgs e)
        {
            this.LogMessage(this.outputData, e.Index, MessageLevel.Information);
        }

        private void LogMessage<T>(PSDataCollection<T> container, int index, MessageLevel level)
            where T : class
        {
            if (container != null)
            {
                var obj = container[index];
                if (obj != null)
                {
                    var text = obj.ToString();
                    if (!string.IsNullOrEmpty(text))
                        this.OnLogReceived(new LogReceivedEventArgs(text, level));
                }
            }
        }
    }
}
