using System;
using System.ComponentModel;
using System.Linq;
using Inedo.Agents;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMaster.Extensibility.VariableFunctions;
#elif Otter
using Inedo.Otter;
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Extensibility.VariableFunctions;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.VariableFunctions;
#endif
using Inedo.Extensions.Windows.PowerShell;
using Inedo.Documentation;
using Inedo.ExecutionEngine;

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

#if BuildMaster
        public override RuntimeValue Evaluate(IGenericBuildMasterContext context)
#elif Otter
        public override RuntimeValue Evaluate(IOtterContext context)
#elif Hedgehog
        public override RuntimeValue Evaluate(IVariableFunctionContext context)
#endif
        {
            var execContext = context as IOperationExecutionContext;
            if (execContext == null)
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
                return new RuntimeValue(result.Output.Select(o => new RuntimeValue(o)));
        }
    }
}
