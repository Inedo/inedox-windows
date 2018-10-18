using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

        [Persistent]
        [DefaultValue(true)]
        [ScriptAlias("Exists")]
        public override bool Exists { get; set; } = true;

        public override string ConfigurationKey => this.GetDisplayPath() + "::" + this.ValueName;

        public override ComparisonResult Compare(PersistedConfiguration other)
        {
            if (!(other is RegistryValueConfiguration reg))
                throw new ArgumentException("Cannot compare configurations of different types.");

            var differences = new List<Difference>();
            if (!this.Exists || !reg.Exists)
            {
                if (this.Exists || reg.Exists)
                {
                    differences.Add(new Difference(nameof(Exists), this.Exists, reg.Exists));
                }

                return new ComparisonResult(differences);
            }

            if (this.ValueKind != reg.ValueKind)
            {
                differences.Add(new Difference(nameof(ValueKind), this.ValueKind, reg.ValueKind));
            }

            if (!(this.Value ?? Enumerable.Empty<string>()).SequenceEqual(reg.Value ?? Enumerable.Empty<string>()))
            {
                differences.Add(new Difference(nameof(Value), string.Join("\n", this.Value), string.Join("\n", reg.Value)));
            }

            return new ComparisonResult(differences);
        }
    }
}
