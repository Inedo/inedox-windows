﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using Inedo.Serialization;

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
        [DefaultValue(InedoRegistryValueKind.String)]
        public InedoRegistryValueKind ValueKind { get; set; } = InedoRegistryValueKind.String;

        [Persistent]
        [DefaultValue(true)]
        [ScriptAlias("Exists")]
        public override bool Exists { get; set; } = true;

        public override string ConfigurationKey => this.GetDisplayPath() + "::" + this.ValueName;

        public override Task<ComparisonResult> CompareAsync(PersistedConfiguration other, IOperationCollectionContext context)
        {
            if (other is not RegistryValueConfiguration reg)
                throw new ArgumentException("Cannot compare configurations of different types.");

            var differences = new List<Difference>();
            if (!this.Exists || !reg.Exists)
            {
                if (this.Exists || reg.Exists)
                {
                    differences.Add(new Difference(nameof(Exists), this.Exists, reg.Exists));
                }

                return Task.FromResult(new ComparisonResult(differences));
            }

            if (this.ValueKind != reg.ValueKind)
                differences.Add(new Difference(nameof(ValueKind), this.ValueKind, reg.ValueKind));

            if (!(this.Value ?? Enumerable.Empty<string>()).SequenceEqual(reg.Value ?? Enumerable.Empty<string>()))
                differences.Add(new Difference(nameof(Value), string.Join("\n", this.Value), string.Join("\n", reg.Value)));

            return Task.FromResult(new ComparisonResult(differences));
        }
    }
}
