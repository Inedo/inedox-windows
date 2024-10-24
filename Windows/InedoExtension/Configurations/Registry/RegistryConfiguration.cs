using System;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Serialization;

namespace Inedo.Extensions.Windows.Configurations.Registry;

[Serializable]
public abstract class RegistryConfiguration : PersistedConfiguration, IExistential
{
    private protected RegistryConfiguration()
    {
    }

    [Undisclosed]
    [Persistent]
    [ScriptAlias("Hive")]
    public InedoRegistryHive Hive { get; set; }
    [Undisclosed]
    [Persistent]
    [ScriptAlias("Key")]
    public string Key { get; set; }
    [Persistent]
    [Required]
    [ConfigurationKey]
    [ScriptAlias("Path")]
    public string Path { get; set; }
    [Persistent]
    public abstract bool Exists { get; set; }

    public string GetDisplayPath() => this.Path ?? (this.Hive.GetAbbreviation() + ":" + GetCanonicalKey(this.Key));

    public static string GetCanonicalKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        return string.Join("\\", key.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries));
    }
}
