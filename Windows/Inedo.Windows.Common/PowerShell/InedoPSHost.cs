using System;
using System.Globalization;
using System.Management.Automation.Host;
using Inedo.Diagnostics;

namespace Inedo.Extensions.Windows.PowerShell
{
    internal sealed class InedoPSHost : PSHost, ILogger
    {
        private readonly InedoPSHostUserInterface ui = new InedoPSHostUserInterface();

        public InedoPSHost()
        {
            this.ui.MessageLogged += this.Ui_MessageLogged;
        }

        public event EventHandler<LogMessageEventArgs> MessageLogged;
        public event EventHandler<ShouldExitEventArgs> ShouldExit;

        public override CultureInfo CurrentCulture => CultureInfo.CurrentCulture;
        public override CultureInfo CurrentUICulture => CultureInfo.CurrentUICulture;
        public override Guid InstanceId { get; } = Guid.NewGuid();
        public override string Name => nameof(InedoPSHost);
        public override Version Version => typeof(InedoPSHost).Assembly.GetName().Version;
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
        public override void NotifyEndApplication() => this.ui.FlushLine();

        public override void SetShouldExit(int exitCode) => this.ShouldExit?.Invoke(this, new ShouldExitEventArgs(exitCode));
        public void Log(MessageLevel logLevel, string message) => this.MessageLogged?.Invoke(this, new LogMessageEventArgs(logLevel, message));

        private void Ui_MessageLogged(object sender, LogMessageEventArgs e) => this.Log(e.Level, e.Message);
    }
}
