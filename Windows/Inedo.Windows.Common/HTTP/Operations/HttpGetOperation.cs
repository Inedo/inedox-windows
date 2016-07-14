using System;
using System.ComponentModel;
using System.Net;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Diagnostics;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
#endif

namespace Inedo.Extensions.HTTP.Operations
{
    [DisplayName("HTTP GET Request")]
    [Description("Executes an HTTP GET, DELETE, or HEAD request against a URL, typically used for RESTful operations.")]
    [Tag(Tags.Http)]
    [ScriptAlias("Get-Http")]
    [ScriptNamespace(Namespaces.Http, PreferUnqualified = true)]
    [DefaultProperty(nameof(Url))]
    [Example(@"
# downloads the http://httpbin.org/get page and stores its contents in the 
# $ResponseBody variable, failing only on 500 errors
Get-Http http://httpbin.org/get
(
    ErrorStatusCodes: 500:599,
    ResponseBody => $ResponseBody
);
")]
    public sealed class HttpGetOperation : HttpOperationBase
    {
        public override string HttpMethod => this.Method.ToString();

        [ScriptAlias("Method")]
        [DefaultValue(GetHttpMethod.GET)]
        public GetHttpMethod Method { get; set; }
        [Required]
        [ScriptAlias("Url")]
        [DisplayName("URL")]
        public string Url { get; set; }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("HTTP ", AH.CoalesceString(config[nameof(this.Method)], "GET")),
                new RichDescription("from ", new Hilite(config[nameof(this.Url)]))
            );
        }

        public async override Task ExecuteAsync(IOperationExecutionContext context)
        {
            try
            {
                new Uri(this.Url);
            }
            catch (Exception ex)
            {
                this.LogError($"The {this.HttpMethod} request URL \"{this.Url}\" is invalid because: {ex.Message}");
                return;
            }

            this.LogInformation($"Performing HTTP {this.HttpMethod} request to the URL \"{this.Url}\"...");

            var request = WebRequest.CreateHttp(this.Url);
            request.Method = this.HttpMethod;

            WebResponse response;
            try
            {
                response = await request.GetResponseAsync();
            }
            catch (WebException ex) when (ex.Response != null)
            {
                response = ex.Response;
            }

            this.ProcessResponse((HttpWebResponse)response);

            this.LogInformation($"HTTP {this.HttpMethod} request completed.");
        }
    }
}
