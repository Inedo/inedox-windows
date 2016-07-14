using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Diagnostics;
using Inedo.ExecutionEngine;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
#endif

namespace Inedo.Extensions.HTTP.Operations
{
    [DisplayName("HTTP POST to URL")]
    [Description("Executes an HTTP POST/PUT/PATCH request to a URL, typically used for RESTful operations.")]
    [Tag(Tags.Http)]
    [ScriptAlias("Post-Http")]
    [ScriptNamespace(Namespaces.Http, PreferUnqualified = true)]
    [DefaultProperty(nameof(Url))]
    [Example(@"
# posts some key-value pairs to a test service and writes the response body to the BuildMaster execution log
Post-Http http://httpbin.org/post
(
    FormData: %(
        Var1: ""value1"",
        Var2: ""value2""
    ),
    LogResponseBody: true
);
")]
    public sealed class HttpPostOperation : HttpOperationBase
    {
        public override string HttpMethod => this.Method.ToString();

        [ScriptAlias("Method")]
        [DefaultValue(PostHttpMethod.POST)]
        public PostHttpMethod Method { get; set; }
        [Required]
        [ScriptAlias("Url")]
        [DisplayName("URL")]
        public string Url { get; set; }
        [Category("Data")]
        [ScriptAlias("ContentType")]
        [DisplayName("Content type")]
        [DefaultValue("application/x-www-form-urlencoded")]
        public string ContentType { get; set; }
        [Category("Data")]
        [ScriptAlias("TextData")]
        [DisplayName("Request text content")]
        [Description("Direct text input that will be written to the request content body. This will override any form data if both are supplied.")]
        public string PostData { get; set; }
        [Category("Data")]
        [ScriptAlias("FormData")]
        [DisplayName("Form data")]
        [Description("A map of form data key/value pairs to send. If TextData is supplied, this value is ignored.")]
        public IDictionary<string, RuntimeValue> FormData { get; set; }
        [Category("Options")]
        [ScriptAlias("LogRequestData")]
        [DisplayName("Log request data")]
        public bool LogRequestData { get; set; }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("HTTP ", AH.CoalesceString(config[nameof(this.Method)], "POST")),
                new RichDescription("to ", new Hilite(config[nameof(this.Url)]))
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
            request.ContentType = AH.CoalesceStr(this.ContentType, "application/x-www-form-urlencoded");

            this.LogDebug("Request Content-Type: " + request.ContentType);

            if (this.LogRequestData)
            {
                var buffer = new StringBuilder();
                buffer.Append("Request content: ");

                if (!string.IsNullOrEmpty(this.PostData))
                {
                    buffer.Append(this.PostData);
                }
                else if (this.FormData != null && this.FormData.Count > 0)
                {
                    bool first = true;
                    foreach (var field in this.FormData)
                    {
                        if (!first)
                            buffer.Append('&');
                        else
                            first = false;

                        buffer.Append(Uri.EscapeDataString(field.Key));
                        buffer.Append('=');
                        buffer.Append(Uri.EscapeDataString(field.Value.AsString() ?? string.Empty));
                    }
                }

                this.LogDebug(buffer.ToString());
            }

            if (!string.IsNullOrEmpty(this.PostData))
            {
                using (var requestStream = request.GetRequestStream())
                using (var sw = new StreamWriter(requestStream, InedoLib.UTF8Encoding))
                {
                    sw.Write(this.PostData);
                }
            }
            else if (this.FormData != null && this.FormData.Count > 0)
            {
                using (var requestStream = request.GetRequestStream())
                using (var sw = new StreamWriter(requestStream, InedoLib.UTF8Encoding))
                {
                    bool first = true;

                    foreach (var field in this.FormData)
                    {
                        if (!first)
                            sw.Write('&');
                        else
                            first = false;

                        sw.Write(Uri.EscapeDataString(field.Key));
                        sw.Write('=');
                        sw.Write(Uri.EscapeDataString(field.Value.AsString() ?? string.Empty));
                    }
                }
            }

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
