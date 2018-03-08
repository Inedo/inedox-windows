using System;
using System.Management.Automation;

namespace Inedo.Extensions.Windows.PowerShell
{
    internal sealed class PowerShellOutputEventArgs : EventArgs
    {
        public PowerShellOutputEventArgs(PSObject obj)
        {
            this.Output = obj;
        }

        public PSObject Output { get; }
    }
}
