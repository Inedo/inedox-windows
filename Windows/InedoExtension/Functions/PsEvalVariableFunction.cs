using System;
using System.ComponentModel;
using Inedo.Agents;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.VariableFunctions;
using Inedo.Extensions.Windows.PowerShell;

namespace Inedo.Extensions.Windows.Functions
{
    [ScriptAlias("PSEval")]
    [Description("Returns the result of a PowerShell script.")]
    [Tag("PowerShell")]
    [Example(@"
# set the $NextYear variable to the value of... next year
set $PowershellScript = >>
(Get-Date).year + 1
>>;

set $NextYear = $PSEval($PowershellScript);

Log-Information $NextYear;
")]
    [Category("PowerShell")]
    public sealed partial class PSEvalVariableFunction : VariableFunction
    {
        [DisplayName("script")]
        [VariableFunctionParameter(0)]
        [Description("The PowerShell script to execute. This should be an expression.")]
        public string ScriptText { get; set; }

        public override RuntimeValue Evaluate(IVariableFunctionContext context)
        {
            if (!(context is IOperationExecutionContext execContext))
                throw new NotSupportedException("This function can currently only be used within an execution.");

            var job = new ExecutePowerShellJob
            {
                CollectOutput = true,
                ScriptText = this.ScriptText,
                Variables = PowerShellScriptRunner.ExtractVariables(this.ScriptText, execContext)
            };

            var jobExecuter = execContext.Agent.GetService<IRemoteJobExecuter>();
            var result = (ExecutePowerShellJob.Result)jobExecuter.ExecuteJobAsync(job, execContext.CancellationToken).Result();

            if (result.Output.Count == 1)
                return result.Output[0];
            else
                return new RuntimeValue(result.Output);
        }
    }
}
