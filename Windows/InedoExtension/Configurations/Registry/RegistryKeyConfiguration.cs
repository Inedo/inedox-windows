using System;
using System.ComponentModel;
using Inedo.Extensibility;
using Inedo.Serialization;

namespace Inedo.Extensions.Windows.Configurations.Registry
{
    [Serializable]
    [DisplayName("Registry Key")]
    public sealed class RegistryKeyConfiguration : RegistryConfiguration
    {
        [Persistent]
        [DefaultValue(true)]
        [ScriptAlias("Exists")]
        public override bool Exists { get; set; } = true;

        [Persistent]
        [Category("Advanced")]
        [ScriptAlias("DefaultValue")]
        [DisplayName("Default value")]
        [Description("A key's default value is the legacy unnamed value that every registry key may have. This is rarely used.")]
        public string DefaultValue { get; set; }

        public override string ConfigurationKey => this.GetDisplayPath();
    }
}
