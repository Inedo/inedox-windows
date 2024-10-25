using System.ComponentModel;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Windows.Configurations.Registry;
using Microsoft.Win32;

namespace Inedo.Extensions.Windows.Operations.Registry;

[Serializable]
[DisplayName("Ensure Registry Key Value")]
[ScriptAlias("Ensure-RegistryKeyValue")]
[ScriptAlias("Ensure-RegistryValue")]
[Description("Ensures that a registry value exists or does not exist on a specified key.")]
[Example(@"Windows::Ensure-RegistryKeyValue
(
    Path: HKLM:SOFTWARE\Inedo\BuildMaster,
    Name: ServicePath,
    Value: C:\BuildMaster\Service,
);")]
[Tag(Tags.Registry)]
public sealed class EnsureRegistryValueOperation : RemoteEnsureOperation<RegistryValueConfiguration>
{
    protected override Task<PersistedConfiguration> RemoteCollectAsync(IRemoteOperationCollectionContext context)
    {
        if (!OperatingSystem.IsWindows())
            throw new ExecutionFailureException("This operation requires Windows.");

        this.LogDebug($"Collecting status of {this.Template.GetDisplayPath()}::{this.Template.ValueName}...");

        var config = new RegistryValueConfiguration
        {
            Hive = this.Template.Hive,
            Key = this.Template.Key,
            ValueName = this.Template.ValueName
        };

        using (var baseKey = RegistryKey.OpenBaseKey((RegistryHive)this.Template.Hive, RegistryView.Default))
        using (var key = baseKey.OpenSubKey(this.Template.Key))
        {
            if (key == null)
            {
                this.LogInformation($"Key {this.Template.GetDisplayPath()} does not exist.");
                config.Exists = false;
            }
            else
            {
                var value = key.GetValue(this.Template.ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (value == null)
                {
                    this.LogInformation($"Value {this.Template.ValueName} does not exist.");
                    config.Exists = false;
                }
                else
                {
                    this.LogInformation($"Value {this.Template.ValueName} exists.");
                    config.Exists = true;
                    config.ValueKind =  (InedoRegistryValueKind)key.GetValueKind(this.Template.ValueName);
                    config.Value = ReadRegistyValue(value, (RegistryValueKind)config.ValueKind);
                }
            }
        }

        return Task.FromResult<PersistedConfiguration>(config);
    }

    protected override Task RemoteConfigureAsync(IRemoteOperationExecutionContext context)
    {
        if (!OperatingSystem.IsWindows())
            throw new ExecutionFailureException("This operation requires Windows.");

        this.LogDebug($"Configuring {this.Template.GetDisplayPath()}::{this.Template.ValueName}...");

        InedoRegistryHive hive;
        string specifiedKey;

        if (string.IsNullOrWhiteSpace(this.Template.Path))
        {
            hive = this.Template.Hive;
            specifiedKey = this.Template.Key;
        }
        else
        {
            if(!this.Template.Path.Contains(':'))
                throw new InvalidOperationException("The Path property must be in the format of Hive:Key");

            hive = this.Template.Path[..this.Template.Path.IndexOf(':')].GetInedoHiveRegistry();
            specifiedKey = this.Template.Path[(this.Template.Path.IndexOf(':')+1)..];
        }


        using (var baseKey = RegistryKey.OpenBaseKey((RegistryHive)hive, RegistryView.Default))
        {
            using (var key = createOrOpenKey())
            {
                if (context.Simulation || key != null)
                {
                    if (this.Template.Exists)
                    {
                        this.LogInformation($"Setting {this.Template.ValueName}...");
                        if (!context.Simulation)
                            key.SetValue(this.Template.ValueName, this.GetRegistryValue(), (RegistryValueKind)this.Template.ValueKind);
                    }
                    else
                    {
                        this.LogInformation($"Deleting {this.Template.ValueName}...");
                        if (!context.Simulation)
                            key.DeleteValue(this.Template.ValueName, false);
                    }
                }
            }

            RegistryKey createOrOpenKey()
            {
                if (this.Template.Exists)
                {
                    this.LogDebug($"Ensuring that {this.Template.GetDisplayPath()} exists...");
                    if (context.Simulation)
                        return baseKey.OpenSubKey(specifiedKey);
                    else
                        return baseKey.CreateSubKey(specifiedKey);
                }
                else
                {
                    this.LogDebug($"Determining if {this.Template.GetDisplayPath()} exists...");
                    return baseKey.OpenSubKey(specifiedKey);
                }
            }
        }

        return Complete();
    }

    protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
    {
        if (string.IsNullOrWhiteSpace((string)config[nameof(RegistryConfiguration.Path)]))
        {
            var hive = (string)config[nameof(RegistryConfiguration.Hive)];
            if (Enum.TryParse<InedoRegistryHive>(hive, true, out var h))
                hive = h.GetAbbreviation();

            return new ExtendedRichDescription(
                new RichDescription(
                    "Ensure Registry Value ",
                    new Hilite(config[nameof(RegistryValueConfiguration.ValueName)]),
                    string.Equals(config[nameof(RegistryConfiguration.Exists)], "false", StringComparison.OrdinalIgnoreCase) ? " does not exist" : " exists"
                ),
                new RichDescription(
                    "in key ",
                    new Hilite(hive + "\\" + RegistryConfiguration.GetCanonicalKey(config[nameof(RegistryConfiguration.Key)]))
               )
            );
        }

        return new ExtendedRichDescription(
               new RichDescription(
                   "Ensure Registry Value ",
                   new Hilite(config[nameof(RegistryValueConfiguration.ValueName)]),
                   string.Equals(config[nameof(RegistryConfiguration.Exists)], "false", StringComparison.OrdinalIgnoreCase) ? " does not exist" : " exists"
               ),
               new RichDescription(
                   "in key ",
                   new Hilite(config[nameof(RegistryConfiguration.Path)])
              )
           );
    }

    [SupportedOSPlatform("windows")]
    private static string[] ReadRegistyValue(object value, RegistryValueKind kind)
    {
        if (value == null)
            return null;

        switch (kind)
        {
            case RegistryValueKind.DWord:
            case RegistryValueKind.QWord:
            case RegistryValueKind.String:
            case RegistryValueKind.ExpandString:
                return new[] { value.ToString() };

            case RegistryValueKind.MultiString:
                return (string[])value;

            case RegistryValueKind.Binary:
                var bytes = (byte[])value;
                var buffer = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                    buffer.Append(b.ToString("X2"));
                return new[] { buffer.ToString() };

            default:
                return null;
        }
    }
    [SupportedOSPlatform("windows")]
    private object GetRegistryValue()
    {
        var s = this.Template.Value.FirstOrDefault();

        switch ((RegistryValueKind)this.Template.ValueKind)
        {
            case RegistryValueKind.String:
            case RegistryValueKind.ExpandString:
                return s ?? string.Empty;

            case RegistryValueKind.DWord:
                return uint.TryParse(s, out uint d) ? d : throw new ExecutionFailureException(s + " is not a valid DWORD value.");

            case RegistryValueKind.QWord:
                return ulong.TryParse(s, out ulong q) ? q : throw new ExecutionFailureException(s + " is not a valid QWORD value.");

            case RegistryValueKind.MultiString:
                return this.Template.Value.ToArray();

            case RegistryValueKind.Binary:
                try
                {
                    var bytes = new byte[s.Length / 2];
                    for (int i = 0; i < bytes.Length; i++)
                        bytes[i] = byte.Parse(s.Substring(i * 2, 2), NumberStyles.HexNumber);
                    return bytes;
                }
                catch
                {
                    throw new ExecutionFailureException("The Binary registry value kind must be formatted as an even number of hexadecimal characters.");
                }

            default:
                throw new ExecutionFailureException($"Registry value kind \"{this.Template.ValueKind}\" is not valid.");
        }
    }
}
