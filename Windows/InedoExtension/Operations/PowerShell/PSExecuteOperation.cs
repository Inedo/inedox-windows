using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Windows.PowerShell;
using Inedo.Web;

namespace Inedo.Extensions.Windows.Operations
{
    [DisplayName("PSExec")]
    [Description("Executes a specified PowerShell script.")]
    [ScriptAlias("Execute-PowerShell")]
    [ScriptAlias("PSExec")]
    [ScriptNamespace(Namespaces.PowerShell, PreferUnqualified = true)]
    [DefaultProperty(nameof(ScriptText))]
    [Tag(Tags.PowerShell)]
    [Note("This operation will inject PowerShell variables from the execution engine runtime that match PowerShell variable expressions. This means you won't get an error if you use an undeclared variable in your script, but some expressions that PowerShell interoplates at runtime (such as a variable inside of a string), cannot be replaced by the operation.")]
    [Note("If you are attempting to write the results of a Format-* call to the  log, you may see "
        + "messages similar to \"Microsoft.PowerShell.Commands.Internal.Format.FormatEntryData\". To convert this to text, "
        + "use the Out-String commandlet at the end of your command chain.")]
    [Note("This script will execute in simulation mode; you set the RunOnSimulation parameter to false to prevent this behavior, or you can use the $IsSimulation variable function within the script.")]
    [Example(@"
# writes the list of services running on the computer to the Otter log
psexec >>
    Get-Service | Where-Object {$_.Status -eq ""Running""} | Format-Table Name, DisplayName | Out-String
>>;

# delete all but the latest 3 logs in the log directory, and log any debug/verbose messages to the Otter log
psexec >>
    Get-ChildItem ""E:\Site\Logs"" | Sort-Object $.CreatedDate -descending | Select-Object -skip 3 | Remove-Item
>> (Verbose: true, Debug: true, RunOnSimulation: false);
")]
    public sealed class PSExecuteOperation : ExecuteOperation
    {
        private PSProgressEventArgs currentProgress;

        [Required]
        [ScriptAlias("Text")]
        [Description("The PowerShell script text.")]
        [DisplayName("Script contents")]
        [DisableVariableExpansion]
        [FieldEditMode(FieldEditMode.Multiline)]
        public string ScriptText { get; set; }

        [ScriptAlias("Debug")]
        [DisplayName("Capture debug")]
        [Description("Captures the PowerShell Write-Debug stream into the Otter debug log. The default is false.")]
        public bool DebugLogging { get; set; }

        [ScriptAlias("Verbose")]
        [DisplayName("Capture verbose")]
        [Description("Captures the PowerShell Write-Verbose stream into the Otter debug log. The default is false.")]
        public bool VerboseLogging { get; set; }

        [ScriptAlias("RunOnSimulation")]
        [DisplayName("Run on simulation")]
        [Description("Indicates whether the script will execute in simulation mode. The default is false.")]
        public bool RunOnSimulation { get; set; }

        [ScriptAlias("Isolated")]
        [Description("When true, the script is run in a temporary AppDomain that is unloaded when the script completes. This is an experimental feature and may decrease performance, but may be useful if a script loads assemblies or other resources that would otherwise be leaked.")]
        public bool Isolated { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            if (context.Simulation && !this.RunOnSimulation)
            {
                this.LogInformation("Executing PowerShell script...");
                return;
            }

            var jobRunner = context.Agent.GetService<IRemoteJobExecuter>();

            var job = new ExecutePowerShellJob
            {
                ScriptText = this.ScriptText,
                DebugLogging = this.DebugLogging,
                VerboseLogging = this.VerboseLogging,
                CollectOutput = false,
                LogOutput = true,
                Variables = PowerShellScriptRunner.ExtractVariables(this.ScriptText, context),
                Isolated = this.Isolated
            };

            job.MessageLogged += (s, e) => this.Log(e.Level, e.Message);

            job.ProgressUpdate += (s, e) => Interlocked.Exchange(ref this.currentProgress, e);

            var result = (ExecutePowerShellJob.Result)await jobRunner.ExecuteJobAsync(job, context.CancellationToken);
            if (result.ExitCode != null)
                this.LogDebug("Script exit code: " + result.ExitCode);
        }

        public override OperationProgress GetProgress()
        {
            var p = this.currentProgress;
            return new OperationProgress(p?.PercentComplete, p?.Activity);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Execute ",
                    new Hilite(config[nameof(this.ScriptText)])
                ),
                new RichDescription(
                    "using Windows PowerShell"
                )
            );
        }
    }
}
