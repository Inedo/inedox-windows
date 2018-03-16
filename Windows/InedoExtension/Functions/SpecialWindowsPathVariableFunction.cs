using System;
using System.ComponentModel;
using Inedo.Agents;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.VariableFunctions;

namespace Inedo.Extensions.Windows.Functions
{
    [ScriptAlias("SpecialWindowsPath")]
    [Description("Returns the full path of a special directory on a Windows system.")]
    public sealed class SpecialWindowsPathVariableFunction : ScalarVariableFunction
    {
        [DisplayName("name")]
        [VariableFunctionParameter(0)]
        [Description("One of the values of the Environment.SpecialFolder enumeration.")]
        public string Name { get; set; }

        protected override object EvaluateScalar(IVariableFunctionContext context)
        {
            if (!Enum.TryParse<Environment.SpecialFolder>(this.Name, out var result))
                throw new ExecutionFailureException("Invalid special folder name: " + this.Name);

            if (context is IOperationExecutionContext c && c.Agent != null)
            {
                var remote = c.Agent.GetService<IRemoteMethodExecuter>();
                return remote.InvokeFunc(Environment.GetFolderPath, result);
            }
            else
            {
                throw new ExecutionFailureException("Server context is required.");
            }
        }
    }
}
