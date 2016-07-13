using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text.RegularExpressions;
using Inedo.BuildMaster.Data;
using Inedo.BuildMaster.Extensibility.Scripting;
using Inedo.Documentation;

namespace Inedo.BuildMasterExtensions.Windows.Scripting.PowerShell
{
    [DisplayName("Windows PowerShell")]
    [Description("Provides script library support for Windows PowerShell scripts.")]
    [Tag("windows")]
    public sealed class PowerShellScriptType : ScriptTypeBase, IScriptMetadataReader
    {
        private static readonly Lazy<Regex> DocumentationRegex = new Lazy<Regex>(() => new Regex(@"\s*\.(?<1>\S+)[ \t]*(?<2>[^\r\n]+)?\s*\n(?<3>(.(?!\n\.))+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture));
        private static readonly Lazy<Regex> SpaceCollapseRegex = new Lazy<Regex>(() => new Regex(@"\s*\n\s*", RegexOptions.Singleline));
        private static readonly Lazy<Regex> ParameterTypeRegex = new Lazy<Regex>(() => new Regex(@"^\[?(?<1>[^\]]+)\]?$", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.ExplicitCapture));

        public override string TrueValue
        {
            get { return "$true"; }
        }
        public override string FalseValue
        {
            get { return "$false"; }
        }

        public ScriptMetadata GetScriptMetadata(TextReader scriptText)
        {
            if (scriptText == null)
                throw new ArgumentNullException("scriptText");

            Collection<PSParseError> errors;

            var tokens = PSParser.Tokenize(scriptText.ReadToEnd(), out errors);

            int paramIndex = tokens
                .TakeWhile(t => t.Type != PSTokenType.Keyword || !string.Equals(t.Content, "param", StringComparison.OrdinalIgnoreCase))
                .Count();

            var parameters = this.ScrapeParameters(tokens.Skip(paramIndex + 1)).ToList();

            var documentationToken = tokens
                .Take(paramIndex)
                .Where(t => t.Type == PSTokenType.Comment && t.Content != null && t.Content.StartsWith("<#") && t.Content.EndsWith("#>"))
                .LastOrDefault();

            if (documentationToken != null)
            {
                var documentation = documentationToken.Content;
                if (documentation.StartsWith("<#") && documentation.EndsWith("#>"))
                    documentation = documentation.Substring(2, documentation.Length - 4);

                var docBlocks = DocumentationRegex
                    .Value
                    .Matches(documentation)
                    .Cast<Match>()
                    .Select(m => new
                        {
                            Name = m.Groups[1].Value,
                            Arg = !string.IsNullOrWhiteSpace(m.Groups[2].Value) ? m.Groups[2].Value.Trim() : null,
                            Value = !string.IsNullOrWhiteSpace(m.Groups[3].Value) ? SpaceCollapseRegex.Value.Replace(m.Groups[3].Value.Trim(), " ") : null
                        })
                    .Where(d => d.Value != null)
                    .ToLookup(
                        d => d.Name,
                        d => new { d.Arg, d.Value },
                        StringComparer.OrdinalIgnoreCase);

                return new ScriptMetadata(
                    description: docBlocks["SYNOPSIS"].Concat(docBlocks["DESCRIPTION"]).Select(d => d.Value).FirstOrDefault(),
                    parameters: parameters.GroupJoin(
                        docBlocks["PARAMETER"],
                        p => p.Name,
                        d => d.Arg,
                        (p, d) => new ScriptParameterMetadata(
                            name: p.Name,
                            description: d.Select(t => t.Value).FirstOrDefault(),
                            defaultValue: p.DefaultValue,
                            inputTypeCode: p.IsBooleanOrSwitch ? Domains.ScriptParameterTypes.CheckBox : null
                        ),
                        StringComparer.OrdinalIgnoreCase)
                );
            }

            return new ScriptMetadata(
                parameters: parameters.Select(p => new ScriptParameterMetadata(p.Name))
            );
        }

        public override IActiveScript ExecuteScript(ScriptExecutionContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            var ps = new PowerShellScriptRunner();
            try
            {
                string fullText;

                using (var reader = GetTextReader2(context))
                {
                    fullText = reader.ReadToEnd();
                }

                ps.PowerShell.AddScript(fullText);

                Collection<PSParseError> errors;
                var tokens = PSParser.Tokenize(fullText, out errors);

                int paramIndex = tokens
                    .TakeWhile(t => t.Type != PSTokenType.Keyword || !string.Equals(t.Content, "param", StringComparison.OrdinalIgnoreCase))
                    .Count();

                var parameters = context
                    .Arguments
                    .GroupJoin(
                        this.ScrapeParameters(tokens.Skip(paramIndex + 1)),
                        a => a.Key,
                        p => p.Name,
                        (a, p) => new { Name = a.Key, a.Value, Metadata = p.FirstOrDefault() },
                        StringComparer.OrdinalIgnoreCase);

                foreach (var arg in parameters)
                {
                    if (arg.Metadata != null)
                    {
                        if (arg.Metadata.DefaultValue == null || !string.IsNullOrEmpty(arg.Value))
                        {
                            if (string.Equals(arg.Metadata.Type, "switch", StringComparison.OrdinalIgnoreCase))
                            {
                                if (string.Equals(arg.Value, this.TrueValue, StringComparison.OrdinalIgnoreCase))
                                    ps.PowerShell.AddParameter(arg.Name);
                            }
                            else if (string.Equals(arg.Metadata.Type, "bool", StringComparison.OrdinalIgnoreCase) || string.Equals(arg.Metadata.Type, "System.Boolean", StringComparison.OrdinalIgnoreCase))
                            {
                                ps.PowerShell.AddParameter(arg.Name, string.Equals(arg.Value, this.TrueValue, StringComparison.OrdinalIgnoreCase));
                            }
                            else
                            {
                                ps.PowerShell.AddParameter(arg.Name, arg.Value);
                            }
                        }
                    }
                    else
                    {
                        ps.PowerShell.AddParameter(arg.Name, arg.Value);
                    }
                }

                foreach (var var in context.Variables)
                    ps.PowerShell.Runspace.SessionStateProxy.SetVariable(var.Key, var.Value);

                return ps;
            }
            catch
            {
                ps?.Dispose();
                throw;
            }
        }

        private static TextReader GetTextReader2(ScriptExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(context.FileName))
                return context.GetTextReader();

            var correctedPath = Path.Combine(context.WorkingDirectory ?? string.Empty, context.FileName);
            return new StreamReader(new FileStream(correctedPath, FileMode.Open, FileAccess.Read, FileShare.Read));
        }

        private IEnumerable<ParamInfo> ScrapeParameters(IEnumerable<PSToken> tokens)
        {
            int groupDepth = 0;
            var paramTokens = new List<PSToken>();

            var filteredTokens = tokens
                .Where(t => t.Type != PSTokenType.Comment && t.Type != PSTokenType.NewLine);

            foreach (var token in filteredTokens)
            {
                paramTokens.Add(token);

                if (token.Type == PSTokenType.GroupStart && token.Content == "(")
                    groupDepth++;

                if (token.Type == PSTokenType.GroupEnd && token.Content == ")")
                {
                    groupDepth--;
                    if (groupDepth <= 0)
                        break;
                }
            }

            var currentParam = new ParamInfo();

            bool expectDefaultValue = false;

            foreach (var token in paramTokens)
            {
                if (token.Type == PSTokenType.Operator && token.Content != "=")
                {
                    expectDefaultValue = false;
                    if (currentParam.Name != null)
                        yield return currentParam;

                    currentParam = new ParamInfo();
                    continue;
                }

                if (expectDefaultValue)
                {
                    currentParam.DefaultValue = token.Content;
                    expectDefaultValue = false;
                    continue;
                }

                if (token.Type == PSTokenType.Type)
                {
                    var match = ParameterTypeRegex.Value.Match(token.Content ?? string.Empty);
                    if (match.Success)
                        currentParam.Type = match.Groups[1].Value;
                }

                if (token.Type == PSTokenType.Variable)
                    currentParam.Name = token.Content;

                if (token.Type == PSTokenType.Operator && token.Content == "=")
                {
                    expectDefaultValue = true;
                    continue;
                }
            }

            if (currentParam.Name != null)
                yield return currentParam;
        }

        private sealed class ParamInfo
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string DefaultValue { get; set; }
            public bool IsBooleanOrSwitch
            {
                get
                {
                    return string.Equals(this.Type, "switch", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(this.Type, "bool", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(this.Type, "System.Boolean", StringComparison.OrdinalIgnoreCase);
                }
            }

            public override string ToString()
            {
                if (this.Type != null)
                    return "[" + this.Type + "] " + this.Name;
                else
                    return this.Name;
            }
        }
    }
}
