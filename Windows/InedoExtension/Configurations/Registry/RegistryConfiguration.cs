using System;
using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Serialization;
using Microsoft.Win32;

namespace Inedo.Extensions.Windows.Configurations.Registry
{
    [Serializable]
    public abstract class RegistryConfiguration : PersistedConfiguration, IExistential
    {
        private protected RegistryConfiguration()
        {
        }

        [Required]
        [Persistent]
        [ConfigurationKey]
        [ScriptAlias("Hive")]
        public RegistryHive Hive { get; set; }
        [Required]
        [Persistent]
        [ConfigurationKey]
        [ScriptAlias("Key")]
        public string Key { get; set; }
        [Persistent]
        public abstract bool Exists { get; set; }

        public string GetDisplayPath() => this.Hive.GetAbbreviation() + "\\" + GetCanonicalKey(this.Key);

        public static string GetCanonicalKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            return string.Join("\\", key.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
