using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Security;
using System.Text;
using Inedo.Diagnostics;

namespace Inedo.Extensions.Windows.PowerShell
{
    internal sealed class InedoPSHostUserInterface : PSHostUserInterface, ILogger
    {
        private readonly StringBuilder lineBuffer = new StringBuilder();
        private readonly InedoPSHostRawUserInterface raw = new InedoPSHostRawUserInterface();

        public event EventHandler<LogMessageEventArgs> MessageLogged;

        public override PSHostRawUserInterface RawUI => this.raw;

        public override Dictionary<string, PSObject> Prompt(string caption, string message, Collection<FieldDescription> descriptions)
        {
            throw new NotImplementedException();
        }
        public override int PromptForChoice(string caption, string message, Collection<ChoiceDescription> choices, int defaultChoice)
        {
            throw new NotImplementedException();
        }
        public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName)
        {
            throw new NotImplementedException();
        }
        public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName, PSCredentialTypes allowedCredentialTypes, PSCredentialUIOptions options)
        {
            throw new NotImplementedException();
        }
        public override string ReadLine()
        {
            throw new NotImplementedException();
        }
        public override SecureString ReadLineAsSecureString()
        {
            throw new NotImplementedException();
        }

        public override void WriteLine() => this.WriteLine(Environment.NewLine);
        public override void WriteLine(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value) => this.WriteLine(value);
        public override void Write(string value)
        {
            if (value.Contains("\n"))
            {
                var lines = value.Split('\n');
                for (int i = 0; i < lines.Length - 1; i++)
                    this.WriteLine(lines[i]);

                this.Write(lines[lines.Length - 1]);
            }

            lock (this.lineBuffer)
            {
                this.lineBuffer.Append(value);
            }
        }
        public override void Write(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value) => this.Write(value);
        public override void WriteLine(string value)
        {
            var line = value;
            lock (this.lineBuffer)
            {
                if (this.lineBuffer.Length > 0)
                {
                    this.lineBuffer.Append(value);
                    line = this.lineBuffer.ToString();
                    this.lineBuffer.Clear();
                }
            }

            this.Log(MessageLevel.Debug, line);
        }
        public void FlushLine()
        {
            string message = null;
            lock (this.lineBuffer)
            {
                if (this.lineBuffer.Length > 0)
                {
                    message = this.lineBuffer.ToString();
                    this.lineBuffer.Clear();
                }
            }

            if (message != null)
                this.Log(MessageLevel.Debug, message);
        }

        public override void WriteDebugLine(string message)
        {
        }
        public override void WriteVerboseLine(string message)
        {
        }
        public override void WriteWarningLine(string message)
        {
        }
        public override void WriteErrorLine(string value)
        {
        }
        public override void WriteProgress(long sourceId, ProgressRecord record)
        {
        }

        public void Log(MessageLevel logLevel, string message) => this.MessageLogged?.Invoke(this, new LogMessageEventArgs(logLevel, message));
    }
}
