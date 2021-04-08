using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Agents;
using Inedo.Extensibility.Operations;
using Inedo.IO;

namespace Inedo.Extensions.Windows.Operations
{
    [ScriptAlias("Sign-Exe")]
    [ScriptAlias("Sign-Dll")]
    [DisplayName("Sign Binary")]
    [Description("Signs .exe or .dll files using an installed code signing certificate.")]
    [Tag("windows")]
    [DefaultProperty(nameof(Includes))]
    [ScriptNamespace("Windows", PreferUnqualified = true)]
    public sealed class SignBinaryOperation : ExecuteOperation
    {
        [Required]
        [ScriptAlias("SubjectName")]
        [DisplayName("Subject")]
        [Description("The subject name of the certificate. This is used to identify the certificate.")]
        public string SubjectName { get; set; }
        [ScriptAlias("TimestampServer")]
        [DisplayName("Timestamp server")]
        [Description("This server will be used to add a timestamp to the signature.")]
        [DefaultValue("http://timestamp.comodoca.com/")]
        public string TimestampServer { get; set; }
        [ScriptAlias("ContentDescription")]
        [DisplayName("Description")]
        [Description("The content description that will be included with the signature.")]
        public string ContentDescription { get; set; }
        [ScriptAlias("ContentUrl")]
        [DisplayName("URL")]
        [Description("The content URL that will be included with the signature.")]
        public string ContentUrl { get; set; }
        [Required]
        [ScriptAlias("Include")]
        [MaskingDescription]
        public IEnumerable<string> Includes { get; set; }
        [ScriptAlias("Exclude")]
        [MaskingDescription]
        public IEnumerable<string> Excludes { get; set; }
        [ScriptAlias("SignToolPath")]
        [DefaultValue("$SignToolPath")]
        [DisplayName("signtool.exe path")]
        [Description("The full path of signtool.exe.")]
        public string SignToolPath { get; set; }
        [ScriptAlias("SourceDirectory")]
        [DisplayName("Directory")]
        public string SourceDirectory { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var sourceDirectory = context.ResolvePath(this.SourceDirectory);

            this.LogDebug($"Signing files in {sourceDirectory}...");

            var fileOps = context.Agent.GetService<IFileOperationsExecuter>();
            var matches = fileOps.GetFileSystemInfos(sourceDirectory, new MaskingContext(this.Includes, this.Excludes))
                .OfType<SlimFileInfo>()
                .ToList();

            if (matches.Count == 0)
            {
                this.LogWarning("No files found which match the specified criteria.");
                return;
            }

            var args = new StringBuilder("sign /sm");
            args.Append($" /n \"{this.SubjectName}\"");

            if (!string.IsNullOrEmpty(this.TimestampServer))
                args.Append($" /t \"{this.TimestampServer}\"");

            if (!string.IsNullOrEmpty(this.ContentDescription))
                args.Append($" /d \"{this.ContentDescription}\"");

            if (!string.IsNullOrEmpty(this.ContentUrl))
                args.Append($" /du \"{this.ContentUrl}\"");

            var signToolPath = this.GetSignToolPath(context.Agent);
            if (signToolPath != null)
            {
                if (!fileOps.FileExists(signToolPath))
                {
                    this.LogError("Cannot find signtool.exe at: " + signToolPath);
                    return;
                }

                foreach (var match in matches)
                {
                    var startInfo = new RemoteProcessStartInfo
                    {
                        FileName = signToolPath,
                        Arguments = args + " \"" + match.FullName + "\"",
                        WorkingDirectory = sourceDirectory
                    };

                    this.LogInformation($"Signing {match.FullName}...");

                    int exitCode = await this.ExecuteCommandLineAsync(context, startInfo);
                    if (exitCode != 0)
                        this.LogError("Signtool.exe returned exit code " + exitCode);
                }
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Sign ",
                    new MaskHilite(config[nameof(Includes)], config[nameof(Excludes)])
                ),
                new RichDescription(
                    "using the ",
                    new Hilite(config[nameof(SubjectName)]),
                    " certificate"
                )
            );
        }

        private string GetSignToolPath(Agent agent)
        {
            if (!string.IsNullOrWhiteSpace(this.SignToolPath))
            {
                this.LogDebug("Signtool path: " + this.SignToolPath);
                return this.SignToolPath;
            }

            this.LogInformation("$SignToolPath variable is not set. Attempting to find latest version from the registry...");
            var signToolPath = agent.GetService<IRemoteMethodExecuter>().InvokeFunc(GetSignToolPathRemote);
            if (string.IsNullOrWhiteSpace(signToolPath))
            {
                this.LogError("Could not determine SignToolPath value on this server. To resolve this issue, ensure that signtool.exe is available on this server and create a server-scoped variable named $SignToolPath set to the location of the signtool.exe file.");
                return null;
            }

            signToolPath = PathEx.Combine(signToolPath, "bin", "signtool.exe");

            this.LogDebug("Signtool path: " + signToolPath);
            return signToolPath;
        }

        private static string GetSignToolPathRemote()
        {
            try
            {
                return GetWindowsSdkInstallRoot();
            }
            catch
            {
                return null;
            }
        }

        private static string GetWindowsSdkInstallRoot()
        {
            using var windowsKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SDKs\Windows", false);
            if (windowsKey == null)
                return null;

            // Later versions of the SDK have this value, but it might not always be there.
            var installFolder = windowsKey.GetValue("CurrentInstallFolder") as string;
            if (!string.IsNullOrEmpty(installFolder))
                return installFolder;

            var subkeys = windowsKey.GetSubKeyNames();
            if (subkeys.Length == 0)
                return null;

            var versionMatch = new Regex(@"\d+\.\d+", RegexOptions.Singleline);

            // Sort subkeys to find the highest version number.
            Array.Sort(subkeys, (a, b) =>
            {
                var aMatch = versionMatch.Match(a);
                var bMatch = versionMatch.Match(b);
                if (!aMatch.Success && !bMatch.Success)
                    return 0;
                else if (!bMatch.Success)
                    return -1;
                else if (!aMatch.Success)
                    return 1;
                else
                    return -new Version(aMatch.Value).CompareTo(new Version(bMatch.Value));
            });

            using var versionKey = windowsKey.OpenSubKey(subkeys[0], false);
            return versionKey.GetValue("InstallationFolder") as string;
        }
    }
}
