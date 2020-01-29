using System;
using System.Collections.Generic;
using System.Linq;
using Inedo.ExecutionEngine;
using Inedo.Extensibility.RaftRepositories;
using Inedo.Extensions.Windows.PowerShell;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;
using Inedo.Web.DP;
using Inedo.Web.Plans;

namespace Inedo.Extensions.Windows.Operations.PowerShell
{
    internal sealed class NullOperationContext : IOperationEditorContext
    {
        public static readonly IOperationEditorContext Instance = new NullOperationContext();

        public int? PlanId => null;
        public string PlanName => null;
        public RaftItemType? PlanType => null;
        public int? RaftId => null;
        public int? ProjectId => null;
    }

    internal sealed class PSCallOperationEditor : OperationEditor
    {
        public PSCallOperationEditor()
            : base(typeof(PSCallOperation), NullOperationContext.Instance)
        {
        }

        public class Argument
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string DefaultValue { get; set; }
            public string Value { get; set; }
            public bool IsBooleanOrSwitch { get; set; }
            public bool IsOutput { get; set; }
        }
        public class PSCallOperationModel
        {
            public string ScriptName { get; set; }
            public IEnumerable<Argument> Arguments { get; set; }
        }

        public override Type ModelType => typeof(PSCallOperationModel);

        public override ISimpleControl CreateView(ActionStatement action)
        {
            if (action.PositionalArguments?.Count != 1)
                return new LiteralHtml("Cannot edit this statement; the target script name is not present.");

            var scriptName = LooselyQualifiedName.Parse(action.PositionalArguments[0]);
            var info = PowerShellScriptInfo.TryLoad(scriptName);
            if (info == null)
                return new LiteralHtml("Cannot edit this statement; script metatdata could not be parsed.");

            var argumentName = new KoElement(KoBind.text(nameof(Argument.Name)));
            var argumentDesc = new KoElement(
                KoBind.text(nameof(Argument.Description))
            );

            var field = new SlimFormField(new LiteralHtml(argumentName.ToString()));
            field.HelpText = new LiteralHtml(argumentDesc.ToString());
            field.Controls.Add(
                new Element("input",
                    new KoBindAttribute("planargvalue", nameof(Argument.Value)))
                { Attributes = { ["type"] = "text" } });

            return new SimpleVirtualCompositeControl(
                new SlimFormField("Script name:", info.Name ?? scriptName.ToString()),
                new Div(
                    new KoElement(
                        KoBind.@foreach(nameof(PSCallOperationModel.Arguments)),
                        field
                    )
                )
                { Class = "argument-container" },
                new SlimFormField(
                    "Parameters:",
                    KoBind.visible($"{nameof(PSCallOperationModel.Arguments)}().length == 0"),
                    "This script does not have any input or output parameters."
                )
            );

        }
        protected override object CreateModel(ActionStatement action)
        {
            if (action.PositionalArguments?.Count != 1)
                return null;

            var scriptName = LooselyQualifiedName.Parse(action.PositionalArguments[0]);

            var info = PowerShellScriptInfo.TryLoad(scriptName);
            if (info == null)
                return null;

            return new PSCallOperationModel
            {
                ScriptName = scriptName.ToString(),
                Arguments = info.Parameters.Select(p => new Argument
                {
                    DefaultValue = p.DefaultValue,
                    Description = p.Description,
                    IsBooleanOrSwitch = p.IsBooleanOrSwitch,
                    IsOutput = p.IsOutput,
                    Name = p.Name,
                    Value = p.IsOutput ? action.OutArguments.GetValueOrDefault(p.Name)?.ToString() : action.Arguments.GetValueOrDefault(p.Name)
                })
            };
        }

#pragma warning disable CS0672 // Member overrides obsolete member
        public override ActionStatement CreateActionStatement(
            QualifiedName actionName,
            object _model)
#pragma warning restore CS0672 // Member overrides obsolete member
        {
            var model = (PSCallOperationModel)_model;
            return new ActionStatement("PSCall",
                model.Arguments
                    .Where(a => !string.IsNullOrEmpty(a.Value) && !a.IsOutput)
                    .ToDictionary(a => a.Name, a => a.Value),
                new[] { model.ScriptName },
                model.Arguments
                    .Where(a => !string.IsNullOrEmpty(a.Value) && RuntimeVariableName.TryParse(a.Value) != null && a.IsOutput)
                    .ToDictionary(a => a.Name, a => RuntimeVariableName.Parse(a.Value))
            );
        }
    }
}
