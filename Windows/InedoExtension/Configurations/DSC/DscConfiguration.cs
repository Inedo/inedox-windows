using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Inedo.ExecutionEngine;
using Inedo.Extensibility.Configurations;
using Inedo.Serialization;

namespace Inedo.Extensions.Windows.Configurations.DSC
{
    [Serializable]
    [DisplayName("PowerShell Desired State")]
    [Description("A configuration that stores state collected by PowerShell DSC.")]
    public sealed class DscConfiguration : PersistedConfiguration
    {
        /// <summary>
        /// Key name used to manually specify the Otter Configuration Key.
        /// </summary>
        public const string ConfigurationKeyPropertyName = "Otter_ConfigurationKey";

        private Dictionary<string, RuntimeValue> dictionary;

        public DscConfiguration()
        {
        }
        public DscConfiguration(IDictionary<string, RuntimeValue> dictionary)
        {
            if (dictionary != null && dictionary.Count > 0)
                this.dictionary = new Dictionary<string, RuntimeValue>(dictionary, StringComparer.OrdinalIgnoreCase);
        }

        public override string ConfigurationKey => this.ExtractConfigurationKey();
        public override string ConfigurationTypeName => "DSC-" + this.ResourceName;
        public override bool HasEncryptedProperties => false;

        [Persistent]
        public string ConfigurationKeyName { get; set; }
        [Persistent]
        public string ResourceName { get; set; }
        [Persistent]
        public bool InDesiredState { get; set; }
        [Persistent]
        public IEnumerable<DscEntry> Entries
        {
            get
            {
                if (this.dictionary == null || this.dictionary.Count == 0)
                    return null;

                return this.dictionary.Select(e => CreateEntry(e.Key, e.Value));
            }
            set
            {
                var d = new Dictionary<string, RuntimeValue>(StringComparer.OrdinalIgnoreCase);

                foreach (var item in value ?? Enumerable.Empty<DscEntry>())
                {
                    if (string.IsNullOrEmpty(item.Key))
                        continue;

                    d[item.Key] = item.ToRuntimeValue();
                }

                this.dictionary = d.Count > 0 ? d : null;
            }
        }
        [Persistent]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("This is only included for compatibility with serialized instances that used it.", true)]
        public IEnumerable<DictionaryConfigurationEntry> Items
        {
            get => null;
            set
            {
                if (value == null)
                {
                    this.dictionary = null;
                    return;
                }

                this.Entries = value.Select(e => new DscEntry { Key = e.Key, Text = e.Value });
            }
        }

        public override IReadOnlyDictionary<string, string> GetPropertiesForDisplay(bool hideEncrypted)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (this.dictionary != null)
            {
                foreach (var p in this.dictionary)
                    d[p.Key] = p.Value.ToString();
            }

            return d;
        }

        public override ComparisonResult Compare(PersistedConfiguration other)
        {
            bool inDesiredState = this.InDesiredState && other is DscConfiguration dsc && dsc.InDesiredState;

            return inDesiredState ? ComparisonResult.Identical : new ComparisonResult(new[] { new Difference(nameof(InDesiredState), true, false) });
        }

        internal Dictionary<string, RuntimeValue> ToPowerShellDictionary(Dictionary<string, RuntimeValueType> propertyTypes = null)
        {
            if (this.dictionary == null)
                return new Dictionary<string, RuntimeValue>(StringComparer.OrdinalIgnoreCase);

            var d = new Dictionary<string, RuntimeValue>(this.dictionary, StringComparer.OrdinalIgnoreCase);
            d.Remove(ConfigurationKeyPropertyName);

            if (propertyTypes != null)
            {
                var propertiesToExpand = new List<string>();

                foreach (var v in d)
                {
                    if (v.Value.ValueType == RuntimeValueType.Scalar && propertyTypes.GetValueOrDefault(v.Key) == RuntimeValueType.Vector)
                        propertiesToExpand.Add(v.Key);
                }

                foreach (var p in propertiesToExpand)
                    d[p] = new RuntimeValue(new[] { d[p] });
            }

            return d;
        }

        private string ExtractConfigurationKey()
        {
            var keyName = this.ConfigurationKeyName ?? "Name";

            if (this.dictionary.TryGetValue(keyName, out var value) && !string.IsNullOrWhiteSpace(value.AsString()))
                return value.AsString();

            if (keyName == "Name")
            {
                throw new InvalidOperationException("The Name property of the DSC resource was not found and the operation is missing "
                    + $"a \"{ConfigurationKeyPropertyName}\" property whose value is the name of the DSC resource property (or properties) to "
                    + "uniquely identify this configuration."
                );
            }
            else
            {
                throw new InvalidOperationException(
                    $"The \"{keyName}\" configuration key specified with the \"{ConfigurationKeyPropertyName}\" property was not found in the returned DSC resournce."
                );
            }
        }

        private static DscEntry CreateEntry(string key, RuntimeValue value)
        {
            switch (value.ValueType)
            {
                default:
                case RuntimeValueType.Scalar:
                    return new DscEntry
                    {
                        Key = key,
                        Text = value.AsString() ?? string.Empty
                    };

                case RuntimeValueType.Vector:
                    return new DscEntry
                    {
                        Key = key,
                        List = value.AsEnumerable().Select(e => CreateEntry(null, e)).ToList()
                    };

                case RuntimeValueType.Map:
                    return new DscEntry
                    {
                        Key = key,
                        Map = value.AsDictionary().Select(e => CreateEntry(e.Key, e.Value)).ToList()
                    };
            }
        }
    }
}
