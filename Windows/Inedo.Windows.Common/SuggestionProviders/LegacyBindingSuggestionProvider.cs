using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Extensions.Windows.Configurations.IIS;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Web.Controls;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Web.Controls;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Web;
#endif

namespace Inedo.Extensions.Windows.SuggestionProviders
{
    /// <summary>
    /// This suggestion provider is only used to help convert legacy single bindings to the
    /// new "list of maps" format in the Plan Editor UI
    /// </summary>
    public sealed class LegacyBindingSuggestionProvider : ISuggestionProvider
    {
        private static readonly Task<IEnumerable<string>> Empty = Task.FromResult(Enumerable.Empty<string>());

        public Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            if (!string.IsNullOrEmpty(config[nameof(IisSiteConfiguration.Bindings)]))
                return Empty;

            string bindingInfo = config[nameof(IisSiteConfiguration.BindingInformation)];
            string protocol = config[nameof(IisSiteConfiguration.BindingProtocol)];
            if (string.IsNullOrEmpty(bindingInfo) || string.IsNullOrEmpty(protocol))
                return Empty;

            var info = BindingInfo.FromBindingInformation(bindingInfo, protocol, null, null);
            if (info == null)
                return Empty;

            return Task.FromResult(Enumerable.Repeat($@"@(
        %(
            IPAddress: {info.IpAddress}, 
            Port: {info.Port}, 
            HostName: {info.HostName}, 
            Protocol: {info.Protocol}
        )
    )",
                1)
            );
        }
    }
}
