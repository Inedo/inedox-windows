using System.ComponentModel;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Actions;
using Inedo.BuildMaster.Web;
using Inedo.Documentation;
using Inedo.Serialization;

namespace Inedo.BuildMasterExtensions.Windows.Iis
{
    [DisplayName("Create IIS Web Site")]
    [Description("Creates a new web site in IIS 7 or later.")]
    [Tag(Tags.Windows)]
    [Tag("iis")]
    [CustomEditor(typeof(CreateIisWebSiteActionEditor))]
    public sealed class CreateIisWebSiteAction : RemoteActionBase
    {
        /// <summary>
        /// Gets or sets the name of the web site.
        /// </summary>
        [Persistent]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the physical path.
        /// </summary>
        [Persistent]
        public string PhysicalPath { get; set; }

        /// <summary>
        /// Gets or sets the application pool.
        /// </summary>
        [Persistent]
        public string ApplicationPool { get; set; }

        /// <summary>
        /// Gets or sets the port.
        /// </summary>
        [Persistent]
        public string Port { get; set; }

        /// <summary>
        /// Gets or sets the optional hostname header for the web site.
        /// </summary>
        [Persistent]
        public string HostName { get; set; }

        /// <summary>
        /// Gets or sets the option IP address of the web site.
        /// </summary>
        [Persistent]
        public string IPAddress { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the action should be ignored if the Web Site aready exists.
        /// </summary>
        [Persistent]
        public bool OmitActionIfWebSiteExists { get; set; }

        public override ExtendedRichDescription GetActionDescription()
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Create ",
                    new Hilite(this.Name),
                    " IIS Web Site"
                ),
                new RichDescription(
                    "at ",
                    new Hilite(this.PhysicalPath),
                    " using the ",
                    new Hilite(this.ApplicationPool),
                    " application pool on port ",
                    new Hilite(this.Port)
                )
            );
        }

        protected override string ProcessRemoteCommand(string name, string[] args)
        {
            this.LogDebug("Physical path: {0}", this.PhysicalPath);
            this.LogDebug("Application pool: {0}", this.ApplicationPool);

            if (this.OmitActionIfWebSiteExists)
            {
                this.LogDebug($"Checking for existing web site with name: {this.Name}");
                if (IISUtil.Instance.WebSiteExists(this.Name))
                {
                    this.LogInformation($"IIS web site {this.Name} already exists, skipping.");
                    return null;
                }
                this.LogDebug($"IIS did not contain a web site named {this.Name}, creating...");
            }

            int port = string.IsNullOrEmpty(this.Port) ? 80 : (AH.ParseInt(this.Port) ?? 0);
            if (port < 1 || port > ushort.MaxValue)
            {
                this.LogError($"The specified port ({this.Port}) does not resolve to a valid port number.");
                return null;
            }

            var bindingInfo = new IISUtil.BindingInfo(this.HostName, port, this.IPAddress);
            this.LogDebug("Binding Info (IP:Port:Hostname): " + bindingInfo);

            IISUtil.Instance.CreateWebSite(
                this.Name, 
                this.PhysicalPath, 
                this.ApplicationPool, 
                port == 443, 
                bindingInfo
            );

            this.LogInformation($"{this.Name} web site created.");

            return null;
        }

        protected override void Execute()
        {
            this.LogInformation($"Creating IIS web site {this.Name}...");
            this.ExecuteRemoteCommand(null);
        }
    }
}
