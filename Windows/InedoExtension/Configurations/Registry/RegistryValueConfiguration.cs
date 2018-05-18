using System;
using System.Collections.Generic;
using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Serialization;
using Microsoft.Win32;

namespace Inedo.Extensions.Windows.Configurations.Registry
{
    [Serializable]
    [DisplayName("Registry Value")]
    public sealed class RegistryValueConfiguration : RegistryConfiguration
    {
        [Required]
        [Persistent]
        [ConfigurationKey]
        [ScriptAlias("Name")]
        [DisplayName("Value name")]
        public string ValueName { get; set; }
        [Persistent]
        [ScriptAlias("Value")]
        public IEnumerable<string> Value { get; set; }
        [Persistent]
        [ScriptAlias("Kind")]
        [DisplayName("Value kind")]
        [DefaultValue(RegistryValueKind.String)]
        public RegistryValueKind ValueKind { get; set; } = RegistryValueKind.String;

        public override bool Exists { get; set; }
    }
}
