using System;
using System.ComponentModel;
using System.IO;
using Inedo.Agents;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Web;
using Inedo.Documentation;
using Inedo.Serialization;

namespace Inedo.BuildMasterExtensions.Windows.Shell
{
    [DisplayName("Execute CScript")]
    [Description("Runs a script using cscript.exe on the target server.")]
    [Tag("windows")]
    [CustomEditor(typeof(ExecuteCScriptActionEditor))]
    public sealed class ExecuteCScriptAction : AgentBasedActionBase
    {
        [Persistent]
        public string ScriptPath { get; set; }
        [Persistent]
        public string Arguments { get; set; }

        public override ExtendedRichDescription GetActionDescription()
        {
            var longDesc = new RichDescription();
            if (!string.IsNullOrWhiteSpace(this.Arguments))
            {
                longDesc.AppendContent(
                    "with arguments: ",
                    new Hilite(this.Arguments)
                );
            }

            return new ExtendedRichDescription(
                new RichDescription(
                    "Run ",
                    new DirectoryHilite(this.OverriddenSourceDirectory, this.ScriptPath),
                    " using cscript.exe"
                ),
                longDesc
            );
        }

        protected override void Execute()
        {
            this.LogDebug("Arguments: " + this.Arguments);
            this.LogInformation("Executing CScript.exe {0}...", this.ScriptPath);

            var agent = this.Context.Agent.GetService<IRemoteMethodExecuter>();
            var systemPath = agent.InvokeFunc(Environment.GetFolderPath, Environment.SpecialFolder.System);

            var args = "\"" + this.ScriptPath + "\"";
            if (!string.IsNullOrWhiteSpace(this.Arguments))
                args += " " + this.Arguments;

            this.ExecuteCommandLine(Path.Combine(systemPath, "cscript.exe"), args, this.Context.SourceDirectory);

            this.LogInformation("CScript execution complete.");
        }
    }
}
