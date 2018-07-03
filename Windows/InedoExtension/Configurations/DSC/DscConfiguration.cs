using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.ExecutionEngine.Variables;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.Windows.Configurations.DSC
{
    [Serializable]
    [DisplayName("PowerShell Desired State")]
    [Description("A configuration that stores state collected by PowerShell DSC.")]
    public sealed class DscConfiguration : PersistedConfiguration
    {
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
        [DefaultValue("Name")]
        [ScriptAlias("ConfigurationKey")]
        [DisplayName("Otter configuration key")]
        [Description("The name of the DSC property which will be used as the Otter configuration key for the server. If this is not specified, the \"Name\" property is used.")]
        public string ConfigurationKeyName { get; set; }
        [Required]
        [Persistent]
        [ScriptAlias("Name")]
        [DisplayName("Resource")]
        [PlaceholderText("ex: File")]
        public string ResourceName { get; set; }
        [Persistent]
        [ScriptAlias("Module")]
        [DisplayName("Module")]
        [DefaultValue("PSDesiredStateConfiguration")]
        public string ModuleName { get; set; }

        [ScriptAlias("Properties")]
        [ScriptAlias("Property")]
        [DisplayName("Properties")]
        [FieldEditMode(FieldEditMode.Multiline)]
        [Description(@"DSC property hashtable as an OtterScript map. Example: %(DestinationPath: C:\hdars\1000.txt, Contents: test file ensured)")]
        [PlaceholderText("%(...)")]
        public IDictionary<string, RuntimeValue> Properties
        {
            get => new Dictionary<string, RuntimeValue>(this.dictionary ?? new Dictionary<string, RuntimeValue>(), StringComparer.OrdinalIgnoreCase);
            set
            {
                if (value == null || value.Count == 0)
                    this.dictionary = null;
                else
                    this.dictionary = new Dictionary<string, RuntimeValue>(value, StringComparer.OrdinalIgnoreCase);
            }
        }

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

        public static ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            string module = config[nameof(ModuleName)];
            string name = config[nameof(ResourceName)];
            if (!string.IsNullOrEmpty(module) && !string.Equals(module, "PSDesiredStateConfiguration", StringComparison.OrdinalIgnoreCase))
                name = module + "::" + name;

            var keyName = AH.CoalesceString((string)config[nameof(ConfigurationKeyName)], "Name");

            string properties = (string)config[nameof(Properties)] ?? string.Empty;
            if (properties.StartsWith("%("))
            {
                try
                {
                    var ps = ProcessedString.Parse(properties);
                    if (ps.Value is MapTextValue m && m.Map.TryGetValue(keyName, out var mapValue))
                    {
                        return new ExtendedRichDescription(
                            new RichDescription(
                                "Ensure DSC ",
                                new Hilite(name),
                                " Resource"
                            ),
                            new RichDescription(
                                "with " + keyName + " = ",
                                new Hilite(mapValue.ToString())
                            )
                        );
                    }
                }
                catch
                {
                }
            }

            return new ExtendedRichDescription(
                new RichDescription(
                    "Ensure DSC ",
                    new Hilite(name),
                    " Resource"
                ),
                new RichDescription(
                    "with properties = ",
                    new Hilite(properties)
                )
            );
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
            var keyName = AH.CoalesceString(this.ConfigurationKeyName, "Name");

            if (this.dictionary.TryGetValue(keyName, out var value) && !string.IsNullOrWhiteSpace(value.AsString()))
                return value.AsString();

            throw new InvalidOperationException($"The \"{keyName}\" property of the DSC resource was not found. Use the \"ConfigurationKey\" argument for Ensure-DscResource or the \"{Operations.PowerShell.PSDscOperation.ConfigurationKeyPropertyName}\" argument for PSDsc to specify the property which uniquely identifies this resource on the server.");
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
