using System.ComponentModel;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.Documentation;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.BuildMasterExtensions.Windows.Iis
{
    [DisplayName("Create IIS App Pool")]
    [Description("Creates an application pool in IIS 7 or later.")]
    [Tag(Tags.Windows)]
    [Tag("iis")]
    [CustomEditor(typeof(CreateIisAppPoolActionEditor))]
    public sealed class CreateIisAppPoolAction : RemoteActionBase
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        [Persistent]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the user.
        /// </summary>
        [Persistent]
        public string User { get; set; }

        /// <summary>
        /// Gets or sets the password.
        /// </summary>
        [Persistent]
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the app pool should be integrated mode or classic.
        /// </summary>
        [Persistent]
        public bool IntegratedMode { get; set; }

        /// <summary>
        /// Gets or sets the managed runtime version.
        /// </summary>
        /// <remarks>Valid values are v2.0 and v4.0</remarks>
        [Persistent]
        public string ManagedRuntimeVersion { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the action should be ignored if the App Pool aready exists.
        /// </summary>
        [Persistent]
        public bool OmitActionIfPoolExists { get; set; }

        public override ExtendedRichDescription GetActionDescription()
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Create ",
                    new Hilite(this.Name),
                    " IIS Application Pool"
                ),
                new RichDescription(
                    "for ",
                    new Hilite(".NET " + this.ManagedRuntimeVersion),
                    ", ",
                    new Hilite(this.IntegratedMode ? "integrated" : "classic"),
                    " pipeline"
                )
            );
        }

        protected override void Execute()
        {
            this.LogDebug($"Creating application pool {this.Name}...");
            this.ExecuteRemoteCommand(null);
        }

        protected override string ProcessRemoteCommand(string name, string[] args)
        {
            this.LogDebug("User: " + this.User);
            this.LogDebug("Pipeline: {0} ({1})", this.ManagedRuntimeVersion, this.IntegratedMode ? "integrated" : "classic");

            if (this.OmitActionIfPoolExists)
            {
                this.LogDebug($"Checking for existing application pool with name: {this.Name}");
                if (IISUtil.Instance.AppPoolExists(this.Name))
                {
                    this.LogInformation($"IIS application pool \"{this.Name}\" already exists, skipping.");
                    return null;
                }
                else
                {
                    this.LogDebug($"IIS did not contain an application pool named {this.Name}, creating...");
                }
            }

            IISUtil.Instance.CreateAppPool(this.Name, this.User, this.Password, this.IntegratedMode, this.ManagedRuntimeVersion);
            this.LogInformation($"{this.Name} application pool created.");

            return null;
        }
    }
}
