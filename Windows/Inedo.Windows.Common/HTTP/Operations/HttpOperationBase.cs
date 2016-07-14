using System;
using System.ComponentModel;
using System.IO;
using System.Net;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
#endif
using Inedo.Diagnostics;
using Inedo.Documentation;

namespace Inedo.Extensions.HTTP.Operations
{
    public abstract class HttpOperationBase : ExecuteOperation
    {
        private const int MaxResponseLength = 1000;

        protected HttpOperationBase()
        {
        }

        public abstract string HttpMethod { get; }
        [Category("Options")]
        [ScriptAlias("LogResponseBody")]
        [DisplayName("Log response body")]
        [Description("When set to true, the full response body content will be logged to the BuildMaster execution log.")]
        public bool LogResponseBody { get; set; }
        [Category("Options")]
        [DefaultValue("400:599")]
        [ScriptAlias("ErrorStatusCodes")]
        [DisplayName("Error status codes")]
        [Description("Comma-separated status codes (or ranges in the form of start:end) that should indicate this action has failed. "
                    + "For example, a value of \"401,500:599\" will fail on all server errors and also when \"HTTP Unauthorized\" is returned. " 
                    + "The default is 400:599")]
        public string ErrorStatusCodes { get; set; } = "400:599";
        [Category("Options")]
        [Output]
        [ScriptAlias("ResponseBody")]
        [DisplayName("Store response as")]
        [PlaceholderText("Do not store response body as variable")]
        [Description("An optional variable name where the response body should be saved.")]
        public string ResponseBodyVariable { get; set; }

        protected void ProcessResponse(HttpWebResponse response)
        {
            var message = string.Format("Server responded with status code {0} - {1}.", (int)response.StatusCode, AH.CoalesceString(response.StatusDescription, response.StatusCode));

            var errorCodeRanges = StatusCodeRangeList.Parse(this.ErrorStatusCodes);
            if (errorCodeRanges.IsInAnyRange((int)response.StatusCode))
                this.LogError(message);
            else
                this.LogInformation(message);

            if (this.LogResponseBody || !string.IsNullOrEmpty(this.ResponseBodyVariable))
            {
                if (response.ContentLength == 0)
                {
                    this.LogDebug("The Content Length of the response was 0.");
                    return;
                }

                try
                {
                    var text = new StreamReader(response.GetResponseStream()).ReadToEnd();

                    if (!string.IsNullOrEmpty(this.ResponseBodyVariable))
                    {
                        this.LogDebug($"Saving response body to ${this.ResponseBodyVariable} variable...");
                        this.ResponseBodyVariable = text;
                    }

                    if (this.LogResponseBody)
                    {
                        if (text.Length > MaxResponseLength)
                        {
                            text = text.Substring(0, MaxResponseLength);
                            this.LogDebug("The following response Content Body is truncated to {0} characters...", MaxResponseLength);
                        }

                        if (!string.IsNullOrEmpty(text))
                            this.LogInformation("Response Content Body: {0}", text);
                    }
                }
                catch (Exception ex)
                {
                    this.LogWarning("Could not read response Content Body because: {0}", ex.Message);
                }
            }
        }
    }
}
