using System;

namespace Inedo.Extensions.Windows.PowerShell
{
    internal sealed class ShouldExitEventArgs : EventArgs
    {
        public ShouldExitEventArgs(int exitCode)
        {
            this.ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}
