using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Windows.Operations.DotNet
{
    [DisplayName("Ensure AppSetting")]
    [Description("Ensures a .NET application configuration file has the specified appSetting key/value pair.")]
    [ScriptAlias("Ensure-AppSetting")]
    [ScriptNamespace("DotNet")]
    [Tag(".net")]
    [Note("The \"appSettings\" section must exist in the file under the \"configuration\" element in order to ensure the key/value pair is present.")]
    [Example(@"# ensures that the application is configured to use test mode for the example third-party API
DotNet::Ensure-AppSetting(
	File: E:\Website\web.config,
	Key: Accounts.ThirdParty.PaymentApi,
	Value: https://test.example.com/api/v3
);
")]
    public sealed class EnsureAppSettingOperation : EnsureOperation
    {
        [Required]
        [ScriptAlias("File")]
        [DisplayName("Config file path")]
        [Description("The file path of the configuration file, typically web.config or app.config.")]
        public string FileName { get; set; }

        [ConfigurationKey]
        [Required]
        [ScriptAlias("Key")]
        [DisplayName("AppSetting key")]
        public string ConfigurationKey { get; set; }

        [Required]
        [ScriptAlias("Value")]
        [DisplayName("AppSetting value")]
        public string ExpectedValue { get; set; }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Ensure ",
                    new Hilite(config[nameof(ConfigurationKey)]),
                    " = ",
                    new Hilite(config[nameof(ExpectedValue)])
                ),
                new RichDescription(
                    " in ",
                    new DirectoryHilite(config[nameof(FileName)])
                )
            );
        }

        public override async Task<PersistedConfiguration> CollectAsync(IOperationCollectionContext context)
        {
            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>();
            var fileName = context.ResolvePath(this.FileName);

            if (!await fileOps.FileExistsAsync(fileName))
                return this.GetConfiguration(null);

            using (var file = await fileOps.OpenFileAsync(fileName, FileMode.Open, FileAccess.Read))
            {
                var doc = XDocument.Load(file);
                var keyElement = doc.Root
                    .Descendants("appSettings")
                    .Elements("add")
                    .FirstOrDefault(e => string.Equals((string)e.Attribute("key"), this.ConfigurationKey, StringComparison.OrdinalIgnoreCase));

                return this.GetConfiguration((string)keyElement?.Attribute("value"));
            }
        }

        public override async Task ConfigureAsync(IOperationExecutionContext context)
        {
            var fileOps = await context.Agent.GetServiceAsync<IFileOperationsExecuter>();
            var fileName = context.ResolvePath(this.FileName);

            if (context.Simulation && !await fileOps.FileExistsAsync(fileName))
            {
                this.LogWarning("File does not exist and execution is in simulation mode.");
                return;
            }

            XDocument doc;

            using (var file = await fileOps.OpenFileAsync(fileName, FileMode.Open, FileAccess.Read))
            {
                doc = XDocument.Load(file);

                var appSettings = doc.Root.Descendants("appSettings").FirstOrDefault();
                if (appSettings == null)
                {
                    this.LogError("The appSettings element does not exist in " + fileName);
                    return;
                }

                var keyElement = appSettings
                    .Elements("add")
                    .FirstOrDefault(e => string.Equals((string)e.Attribute("key"), this.ConfigurationKey, StringComparison.OrdinalIgnoreCase));

                if (keyElement == null)
                {
                    this.LogDebug("Key was not found, adding...");
                    appSettings.Add(new XElement("add", new XAttribute("key", this.ConfigurationKey), new XAttribute("value", this.ExpectedValue)));
                    this.LogDebug("AppSetting key and value added.");
                }
                else
                {
                    this.LogDebug($"Changing appSetting value to {this.ExpectedValue}...");
                    keyElement.SetAttributeValue("value", this.ExpectedValue);
                    this.LogDebug("AppSetting value changed.");
                }
            }

            if (!context.Simulation)
            {
                using (var file = await fileOps.OpenFileAsync(fileName, FileMode.Create, FileAccess.Write))
                {
                    doc.Save(file);
                }
            }

            this.LogInformation($"AppSetting \"{this.ConfigurationKey}\" set to \"{this.ExpectedValue}\".");
        }

        private KeyValueConfiguration GetConfiguration(string value)
        {
            return new KeyValueConfiguration
            {
                Type = "AppSetting",
                Key = this.FileName + "::" + this.ConfigurationKey,
                Value = value
            };
        }

        public override PersistedConfiguration GetConfigurationTemplate() => this.GetConfiguration(this.ExpectedValue);
    }
}