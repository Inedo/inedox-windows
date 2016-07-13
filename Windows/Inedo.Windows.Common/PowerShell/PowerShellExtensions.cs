using System;
using System.Management.Automation;
using Inedo.Diagnostics;

namespace Inedo.Extensions.Windows.PowerShell
{
    internal static class PowerShellExtensions
    {
        public static void AttachLogging(this PSDataStreams streams, ILogger logger)
        {
            if (streams == null)
                throw new ArgumentNullException(nameof(streams));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            streams.Debug.DataAdded +=
                (s, e) =>
                {
                    var rubbish = streams.Debug[e.Index];
                    logger.LogDebug(rubbish.Message);
                };

            streams.Verbose.DataAdded +=
                (s, e) =>
                {
                    var rubbish = streams.Verbose[e.Index];
                    logger.LogDebug(rubbish.Message);
                };

            streams.Warning.DataAdded +=
                (s, e) =>
                {
                    var rubbish = streams.Warning[e.Index];
                    logger.LogWarning(rubbish.Message);
                };

            streams.Error.DataAdded +=
                (s, e) =>
                {
                    var rubbish = streams.Error[e.Index];
                    if (rubbish != null)
                        logger.LogError(rubbish.ToString());
                };
        }
    }
}
