using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;

namespace Inedo.Extensions.Windows.Operations.IIS.Sites
{
    [DisplayName("Stop Site")]
    [Description("Stops an IIS Site.")]
    [ScriptAlias("Stop-Site")]
    [ScriptNamespace(Namespaces.IIS)]
    public sealed class StopSiteOperation : SiteOperationBase
    {
        internal override SiteOperationType OperationType => SiteOperationType.Stop;

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription(
                    "Stop ",
                    new Hilite(config[nameof(SiteName)]),
                    " IIS Site"
                )
            );
        }
    }
}
