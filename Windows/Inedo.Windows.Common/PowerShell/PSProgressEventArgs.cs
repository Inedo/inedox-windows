using System;

namespace Inedo.Extensions.Windows.PowerShell
{
    internal sealed class PSProgressEventArgs : EventArgs
    {
        public PSProgressEventArgs(int percent, string activity)
        {
            this.PercentComplete = percent;
            this.Activity = activity;
        }

        public int PercentComplete { get; }
        public string Activity { get; }
    }
}
