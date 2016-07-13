using System;
using System.Globalization;
using System.Management.Automation.Host;
using Inedo.Diagnostics;

namespace Inedo.BuildMasterExtensions.Windows.Scripting.PowerShell
{
    internal sealed class BuildMasterPSHost : PSHost, ILogger
    {
        private readonly Guid UniqueId = Guid.NewGuid();
        private readonly BuildMasterPSHostUserInterface ui = new BuildMasterPSHostUserInterface();

        public BuildMasterPSHost()
        {
            this.ui.MessageLogged += this.Ui_MessageLogged;
        }

        public event EventHandler<LogMessageEventArgs> MessageLogged;
        public event EventHandler<ShouldExitEventArgs> ShouldExit;

        public override CultureInfo CurrentCulture => CultureInfo.CurrentCulture;
        public override CultureInfo CurrentUICulture => CultureInfo.CurrentUICulture;
        public override Guid InstanceId => UniqueId;
        public override string Name => "BuildMaster";
        public override Version Version => typeof(BuildMasterPSHost).Assembly.GetName().Version;
        public override PSHostUserInterface UI => this.ui;

        public override void EnterNestedPrompt()
        {
            throw new NotImplementedException();
        }
        public override void ExitNestedPrompt()
        {
            throw new NotImplementedException();
        }

        public override void NotifyBeginApplication()
        {
        }
        public override void NotifyEndApplication()
        {
            this.ui.FlushLine();
        }

        public override void SetShouldExit(int exitCode)
        {
            var handler = this.ShouldExit;
            if (handler != null)
                handler(this, new ShouldExitEventArgs(exitCode));
        }

        public void Log(MessageLevel logLevel, string message)
        {
            var handler = this.MessageLogged;
            if (handler != null)
                handler(this, new LogMessageEventArgs(logLevel, message));
        }

        private void Ui_MessageLogged(object sender, LogMessageEventArgs e)
        {
            this.Log(e.Level, e.Message);
        }
    }
}
