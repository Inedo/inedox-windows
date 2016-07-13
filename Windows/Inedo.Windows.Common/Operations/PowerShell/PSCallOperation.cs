using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMaster.Web;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Web.Controls.Extensions;
#endif
using Inedo.Extensions.Windows.PowerShell;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine;

namespace Inedo.Extensions.Windows.Operations.PowerShell
{
    [DisplayName("PSCall")]
    [Description("Calls a PowerShell Script that is stored as an asset.")]
    [ScriptAlias("PSCall")]
    [Tag("powershell")]
    [ScriptNamespace("PowerShell", PreferUnqualified = true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [CustomEditor(typeof(PSCallOperationEditor))]
    [Example(@"
# execute the hdars.ps1 script, passing Argument1 and Aaaaaarg2 as variables, and capturing the value of OutputArg as $MyVariable
pscall hdars (
  Argument1: hello,
  Aaaaaarg2: World,
  OutputArg => $MyVariable
);
")]
    public sealed class PSCallOperation : ExecuteOperation, ICustomArgumentMapper
    {
        public RuntimeValue DefaultArgument { get; set; }
        public IReadOnlyDictionary<string, RuntimeValue> NamedArguments { get; set; }
        public IDictionary<string, RuntimeValue> OutArguments { get; set; }

        public override Task ExecuteAsync(IOperationExecutionContext context)
        {
            if (context.Simulation)
            {
                this.LogInformation("Executing PowerShell Script...");
                return Complete;
            }

            var fullScriptName = this.DefaultArgument.AsString();
            if (fullScriptName == null)
            {
                this.LogError("Bad or missing script name.");
                return Complete;
            }

            return PSUtil.ExecuteScriptAsync(
                logger: this,
                context: context,
                fullScriptName: fullScriptName,
                arguments: this.NamedArguments,
                outArguments: this.OutArguments,
                collectOutput: false
            );
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            if (string.IsNullOrWhiteSpace(config.DefaultArgument))
                return new ExtendedRichDescription(new RichDescription("PSCall {error parsing statement}"));

            var defaultArg = config.DefaultArgument;
            var longDesc = new RichDescription();

            bool longDescInclused = false;
            var scriptName = QualifiedName.TryParse(defaultArg);
            if (scriptName != null)
            {
                var info = PowerShellScriptInfo.TryLoad(scriptName);
                if (!string.IsNullOrEmpty(info?.Description))
                {
                    longDesc.AppendContent(info.Description);
                    longDescInclused = true;
                }

                var listParams = new List<string>();
                foreach (var prop in config.NamedArguments)
                    listParams.Add($"{prop.Key}: {prop.Value}");

                foreach (var prop in config.OutArguments)
                    listParams.Add($"{prop.Key} => {prop.Value}");

                if (listParams.Count > 0)
                {
                    if (longDescInclused)
                        longDesc.AppendContent(" - ");

                    longDesc.AppendContent(new ListHilite(listParams));
                    longDescInclused = true;
                }
            }

            if (!longDescInclused)
                longDesc.AppendContent("with no parameters");

            return new ExtendedRichDescription(
                new RichDescription("PSCall ", new Hilite(defaultArg)),
                longDesc
            );
        }
    }
}
