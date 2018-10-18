using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Windows.Configurations.Registry;
using Microsoft.Win32;

namespace Inedo.Extensions.Windows.Operations.Registry
{
    [Serializable]
    [DisplayName("Ensure Registry Key")]
    [ScriptAlias("Ensure-RegistryKey")]
    [Description("Ensures that a registry key exists or does not exist.")]
    [Example(@"Windows::Ensure-RegistryKey
(
    Hive: LocalMachine,
    Key: SOFTWARE\Inedo\BuildMaster
);")]
    [Tag(Tags.Registry)]
    public sealed class EnsureRegistryKeyOperation : RemoteEnsureOperation<RegistryKeyConfiguration>
    {
        protected override Task<PersistedConfiguration> RemoteCollectAsync(IRemoteOperationCollectionContext context)
        {
            this.LogDebug($"Collecting status of {this.Template.GetDisplayPath()}...");

            var config = new RegistryKeyConfiguration
            {
                Hive = this.Template.Hive,
                Key = this.Template.Key
            };

            using (var baseKey = RegistryKey.OpenBaseKey(this.Template.Hive, RegistryView.Default))
            using (var key = baseKey.OpenSubKey(this.Template.Key))
            {
                if (key == null)
                {
                    this.LogInformation(this.Template.GetDisplayPath() + " does not exist.");
                    config.Exists = false;
                }
                else
                {
                    config.Exists = true;
                    config.DefaultValue = key.GetValue(null)?.ToString();

                    this.LogInformation(this.Template.GetDisplayPath() + " exists.");
                }
            }

            return Task.FromResult<PersistedConfiguration>(config);
        }

        protected override Task RemoteConfigureAsync(IRemoteOperationExecutionContext context)
        {
            this.LogDebug($"Configuring {this.Template.GetDisplayPath()}...");

            using (var baseKey = RegistryKey.OpenBaseKey(this.Template.Hive, RegistryView.Default))
            {
                if (this.Template.Exists)
                {
                    this.LogInformation($"Ensuring that {this.Template.GetDisplayPath()} exists...");

                    using (var key = createOrOpen())
                    {
                        this.LogInformation(this.Template.GetDisplayPath() + " created.");
                        if (!string.IsNullOrWhiteSpace(this.Template.DefaultValue))
                        {
                            this.LogDebug($"Setting default value to {this.Template.DefaultValue}...");
                            if (!context.Simulation)
                                key.SetValue(null, this.Template.DefaultValue);
                        }
                    }
                }
                else
                {
                    this.LogInformation($"Deleting {this.Template.GetDisplayPath()}...");
                    if (!context.Simulation)
                        baseKey.DeleteSubKeyTree(this.Template.Key, false);
                }

                RegistryKey createOrOpen()
                {
                    if (context.Simulation)
                        return baseKey.OpenSubKey(this.Template.Key);
                    else
                        return baseKey.CreateSubKey(this.Template.Key);
                }
            }

            return Complete();
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            var hive = (string)config[nameof(RegistryConfiguration.Hive)];
            if (Enum.TryParse<RegistryHive>(hive, true, out var h))
                hive = h.GetAbbreviation();

            return new ExtendedRichDescription(
                new RichDescription(
                    "Ensure Registry Key ",
                    new Hilite(h + "\\" + RegistryConfiguration.GetCanonicalKey(config[nameof(RegistryConfiguration.Key)]))
                ),
                new RichDescription(
                    string.Equals(config[nameof(RegistryConfiguration.Exists)], "false", StringComparison.OrdinalIgnoreCase) ? "does not exist" : "exists"
                )
            );
        }
    }
}
