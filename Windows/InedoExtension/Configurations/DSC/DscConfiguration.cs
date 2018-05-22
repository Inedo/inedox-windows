using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using Inedo.ExecutionEngine;
using Inedo.Extensibility.Configurations;
using Inedo.Extensions.Windows.PowerShell;
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

        private Dictionary<string, string> dictionary;

        /// <summary>
        /// Initializes a new instance of the <see cref="DscConfiguration"/> class.
        /// </summary>
        public DscConfiguration()
        {
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="DscConfiguration"/> class.
        /// </summary>
        /// <param name="dictionary">Dictionary to copy.</param>
        /// <param name="template">Template containing configuration key.</param>
        public DscConfiguration(IDictionary<string, string> dictionary)
        {
            var d = dictionary != null ? new Dictionary<string, string>(dictionary, StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (d.Count > 0)
                this.dictionary = d;
        }

        /// <summary>
        /// Gets the unique configuration key.
        /// </summary>
        public override string ConfigurationKey => this.ExtractConfigurationKey();
        /// <summary>
        /// Gets the configuration type.
        /// </summary>
        public override string ConfigurationTypeName => "DSC-" + this.ResourceName;

        /// <summary>
        /// Gets or sets the overridden configuration key name.
        /// </summary>
        [Persistent]
        public string ConfigurationKeyName { get; set; }
        /// <summary>
        /// Gets or sets the DSC resource type.
        /// </summary>
        [Persistent]
        public string ResourceName { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether DSC reported no drift.
        /// </summary>
        [Persistent]
        public bool InDesiredState { get; set; }

        /// <summary>
        /// Gets or sets the items.
        /// </summary>
        [Persistent]
        public IEnumerable<DictionaryConfigurationEntry> Items
        {
            get
            {
                if (this.dictionary == null)
                    return Enumerable.Empty<DictionaryConfigurationEntry>();

                return this.dictionary.Select(e => new DictionaryConfigurationEntry { Key = e.Key, Value = e.Value });
            }
            set
            {
                if (value == null)
                {
                    this.dictionary = null;
                    return;
                }

                this.dictionary = (from e in value
                                   group e by e.Key into g
                                   select g.First()).ToDictionary(e => e.Key, e => e.Value);
            }
        }

        /// <summary>
        /// Returns the properties.
        /// </summary>
        /// <returns>The properties.</returns>
        public override IReadOnlyDictionary<string, string> GetPropertiesForDisplay(bool hideEncrypted) => this.dictionary ?? new Dictionary<string, string>();
        /// <summary>
        /// Gets a value indicating whether this instance has encrypted properties.
        /// </summary>
        public override bool HasEncryptedProperties => false;

        public override ComparisonResult Compare(PersistedConfiguration other)
        {
            bool inDesiredState = this.InDesiredState && other is DscConfiguration dsc && dsc.InDesiredState;

            return inDesiredState ? ComparisonResult.Identical : new ComparisonResult(new[] { new Difference(nameof(InDesiredState), true, false) });
        }

        internal Dictionary<string, object> GetHashTable()
        {
            if (this.dictionary == null)
                return new Dictionary<string, object>();

            var hashTable = new Dictionary<string, object>(this.dictionary.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var item in this.dictionary)
            {
                if (string.Equals(item.Key, ConfigurationKeyPropertyName, StringComparison.OrdinalIgnoreCase))
                    continue;

                hashTable[item.Key] = GetValueLiteral(item.Value);
            }

            return hashTable;
        }

        private string ExtractConfigurationKey()
        {
            var keyName = this.ConfigurationKeyName ?? "Name";

            if (this.dictionary.TryGetValue(keyName, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;

            throw new InvalidOperationException("The Name property of the DSC resource was not found and the operation is missing "
                + $"a \"{ConfigurationKeyPropertyName}\" property whose value is the name of the DSC resource property (or properties) to "
                + "uniquely identify this configuration."
            );
        }

        private static object GetValueLiteral(string value)
        {
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "$true", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "$false", StringComparison.OrdinalIgnoreCase))
                return false;

            if (decimal.TryParse(value, out var maybeDecimal))
                return maybeDecimal;

            if (value.StartsWith("@"))
            {
                var ast = Parser.ParseInput(value, out var tokens, out var errors);
                if (errors.Length == 0)
                {
                    try
                    {
                        var scriptBlock = ast.GetScriptBlock();
                        scriptBlock.CheckRestrictedLanguage(new string[0], new string[0], false);
                        using (var runspace = RunspaceFactory.CreateRunspace(new InedoPSHost()))
                        {
                            runspace.Open();
                            using (var powershell = System.Management.Automation.PowerShell.Create())
                            {
                                powershell.Runspace = runspace;
                                powershell.AddScript("$x = " + value);
                                powershell.Invoke();
                                return powershell.Runspace.SessionStateProxy.GetVariable("x");
                            }
                        }
                    }
                    catch
                    {
                        // Process as a string
                    }
                }
            }

            return PowerShellScriptRunner.ConvertToPSValue(new RuntimeValue(value));
        }
    }
}
