using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Windows.Configurations.Registry;
using Microsoft.Win32;

namespace Inedo.Extensions.Windows.Operations.Registry
{
    [Serializable]
    [DisplayName("Get Registry Value")]
    [ScriptAlias("Get-RegistryValue")]
    [Description("Reads a value from the Windows registry and stores it in a variable.")]
    [Tag(Tags.Registry)]
    [Example(@"Windows::Get-RegistryValue
(
    Hive: LocalMachine,
    Key: SOFTWARE\7-Zip,
    Name: Path,
    Value => $PathTo7Zip
);")]
    public sealed class GetRegistryValueOperation : RemoteExecuteOperation
    {
        [Required]
        [ScriptAlias("Hive")]
        public RegistryHive Hive { get; set; }
        [Required]
        [ScriptAlias("Key")]
        public string Key { get; set; }
        [Required]
        [ScriptAlias("Name")]
        [DisplayName("Value name")]
        public string ValueName { get; set; }
        [Output]
        [ScriptAlias("Value")]
        [DisplayName("Store to variable")]
        public RuntimeValue Value { get; set; }
        [Category("Advanced")]
        [ScriptAlias("FailIfNotFound")]
        [DisplayName("Fail if value not found")]
        public bool FailIfNotFound { get; set; }

        protected override Task<object> RemoteExecuteAsync(IRemoteOperationExecutionContext context)
        {
            using (var baseKey = RegistryKey.OpenBaseKey(this.Hive, RegistryView.Default))
            {
                using (var key = baseKey.OpenSubKey(this.Key))
                {
                    if (key == null)
                    {
                        this.Log(this.FailIfNotFound ? MessageLevel.Error : MessageLevel.Information, $"Key \"{this.Key}\" not found.");
                        return Complete;
                    }

                    var value = key.GetValue(this.ValueName);
                    if (value == null)
                    {
                        this.Log(this.FailIfNotFound ? MessageLevel.Error : MessageLevel.Information, $"Value \"{this.ValueName}\" not found in key \"{this.Key}\".");
                        return Complete;
                    }

                    var kind = key.GetValueKind(this.ValueName);
                    if (kind == RegistryValueKind.MultiString)
                        this.Value = new RuntimeValue(((string[])value).Select(v => new RuntimeValue(v)).ToList());
                    else
                        this.Value = value.ToString();
                }
            }

            return Complete;
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            var hive = (string)config[nameof(Hive)];
            if (Enum.TryParse<RegistryHive>(hive, true, out var h))
                hive = h.GetAbbreviation();

            return new ExtendedRichDescription(
                new RichDescription(
                    "Store Registry Value ",
                    new Hilite(config[nameof(ValueName)]),
                    " to ",
                    new Hilite(config[nameof(Value)])
                ),
                new RichDescription(
                    "from key ",
                    new Hilite(h + "\\" + RegistryConfiguration.GetCanonicalKey(config[nameof(Key)]))
               )
            );
        }
    }
}
