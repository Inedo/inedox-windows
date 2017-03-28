using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Management.Automation;
using System.Text.RegularExpressions;
using Inedo.ExecutionEngine;
#if BuildMaster
using Inedo.BuildMaster.Data;
#elif Otter
using Inedo.Otter.Extensibility.RaftRepositories;
#endif

namespace Inedo.Extensions.Windows.PowerShell
{
    partial class PowerShellScriptInfo
    {
        private static readonly LazyRegex DocumentationRegex = new LazyRegex(@"\s*\.(?<1>\S+)[ \t]*(?<2>[^\r\n]+)?\s*\n(?<3>(.(?!\n\.))+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture);
        private static readonly LazyRegex SpaceCollapseRegex = new LazyRegex(@"\s*\n\s*", RegexOptions.Singleline);
        private static readonly LazyRegex ParameterTypeRegex = new LazyRegex(@"^\[?(?<1>[^\]]+)\]?$", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.ExplicitCapture);

        public static bool TryParse(TextReader scriptText, out PowerShellScriptInfo info)
        {
            try
            {
                info = Parse(scriptText);
                return true;
            }
            catch
            {
                info = null;
                return false;
            }
        }
        public static PowerShellScriptInfo Parse(TextReader scriptText)
        {
            if (scriptText == null)
                throw new ArgumentNullException(nameof(scriptText));

            Collection<PSParseError> errors;

            var tokens = PSParser.Tokenize(scriptText.ReadToEnd(), out errors);

            int paramIndex = tokens
                .TakeWhile(t => t.Type != PSTokenType.Keyword || !string.Equals(t.Content, "param", StringComparison.OrdinalIgnoreCase))
                .Count();

            var parameters = ScrapeParameters(tokens.Skip(paramIndex + 1)).ToList();

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

                return new PowerShellScriptInfo(
                    description: docBlocks["SYNOPSIS"].Concat(docBlocks["DESCRIPTION"]).Select(d => d.Value).FirstOrDefault(),
                    parameters: parameters.GroupJoin(
                        docBlocks["PARAMETER"],
                        p => p.Name,
                        d => d.Arg,
                        (p, d) => new PowerShellParameterInfo(
                            name: p.Name,
                            description: d.Select(t => t.Value).FirstOrDefault(),
                            defaultValue: p.DefaultValue,
                            isBooleanOrSwitch: p.IsBooleanOrSwitch,
                            isOutput: p.IsOutput
                        ),
                        StringComparer.OrdinalIgnoreCase)
                );
            }

            return new PowerShellScriptInfo(
                parameters: parameters.Select(p => new PowerShellParameterInfo(
                    name: p.Name,
                    defaultValue: p.DefaultValue,
                    isBooleanOrSwitch: p.IsBooleanOrSwitch,
                    isOutput: p.IsOutput
                ))
            );
        }
        public static PowerShellScriptInfo TryLoad(LooselyQualifiedName scriptName)
        {
#if BuildMaster

            int? applicationId = null;
            if (!string.IsNullOrWhiteSpace(scriptName.Namespace) && !string.Equals(scriptName.Name, "GLOBAL", StringComparison.OrdinalIgnoreCase))
            {
                applicationId = DB.Applications_GetApplications(null, true)
                    .FirstOrDefault(a => string.Equals(a.Application_Name, scriptName.Namespace, StringComparison.OrdinalIgnoreCase))
                    ?.Application_Id;
            }

            var name = scriptName.Name;
            if (!name.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
                name += ".ps1";

            var script = DB.ScriptAssets_GetScriptByName(name, applicationId);
            if (script == null)
                return null;

            using (var stream = new MemoryStream(script.Script_Text, false))
            using (var reader = new StreamReader(stream, InedoLib.UTF8Encoding))
            {
                PowerShellScriptInfo info;
                TryParse(reader, out info);
                return info;
            }
#elif Otter
            using (var raft = RaftRepository.OpenRaft(scriptName.Namespace ?? RaftRepository.DefaultName))
            using (var item = raft.OpenRaftItem(RaftItemType.Script, scriptName.Name + ".ps1", FileMode.Open, FileAccess.Read))
            {
                if (item == null)
                    return null;

                using (var reader = new StreamReader(item, InedoLib.UTF8Encoding))
                {
                    PowerShellScriptInfo info;
                    if (!TryParse(reader, out info))
                        return null;

                    return info;
                }
            }
#endif
        }
        private static IEnumerable<ParamInfo> ScrapeParameters(IEnumerable<PSToken> tokens)
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
            public bool IsOutput
            {
                get
                {
                    return string.Equals(this.Type, "ref", StringComparison.OrdinalIgnoreCase);
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
