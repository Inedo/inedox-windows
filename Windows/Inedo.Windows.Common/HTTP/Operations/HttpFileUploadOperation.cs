using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Operations;
#endif
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.IO;

namespace Inedo.Extensions.HTTP.Operations
{
    [DisplayName("Upload File to URL")]
    [Description("Uploads a file to a specified URL using an HTTP POST.")]
    [ScriptAlias("Upload-Http")]
    [ScriptNamespace(Namespaces.Http, PreferUnqualified = true)]
    [DefaultProperty(nameof(FileName))]
    [Example(@"
# uploads a file to example.org service endpoint
Upload-Http ReleaseNotes.xml (
    To: http://example.org/upload-service/v3/hdars
);
")]
    public sealed class HttpFileUploadOperation : HttpOperationBase
    {
        [Required]
        [DisplayName("File name")]
        [ScriptAlias("FileName")]
        [Description("The path of the file to upload.")]
        public string FileName { get; set; }
        [Required]
        [DisplayName("To URL")]
        [ScriptAlias("To")]
        [Description("The URL where the file will be uploaded.")]
        public string Url { get; set; }

        public override string HttpMethod => "POST";

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var fileOps = context.Agent.GetService<IFileOperationsExecuter>();

            try
            {
                new Uri(this.Url);
            }
            catch (Exception ex)
            {
                this.LogError($"The URL \"{this.Url}\" is invalid because: {ex.Message}");
                return;
            }

            var fileName = context.ResolvePath(this.FileName);
            this.LogInformation($"Uploading file from \"{fileName}\" to \"{this.Url}\"...");

            if (!fileOps.FileExists(fileName))
            {
                this.LogError($"The file \"{fileName}\" does not exist.");
                return;
            }

            using (var stream = fileOps.OpenFile(fileName, FileMode.Open, FileAccess.Read))
            {
                await this.UploadFileAsync(stream, context.CancellationToken, fileName);
            }

            this.LogInformation("HTTP file upload completed.");
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("HTTP Upload ", new Hilite(config[nameof(this.FileName)])),
                new RichDescription("to ", new Hilite(config[nameof(this.Url)]))
            );
        }

        private async Task UploadFileAsync(Stream sourceStream, CancellationToken cancellationToken, string fileName)
        {
            var boundary = "-------------------------" + DateTime.UtcNow.Ticks.ToString("x");

            var request = WebRequest.CreateHttp(this.Url);
            request.Method = "POST";
            request.AutomaticDecompression = DecompressionMethods.GZip;
            request.ContentType = "multipart/form-data; boundary=" + boundary;
            using (var requestStream = await request.GetRequestStreamAsync())
            {
                var requestWriter = new StreamWriter(requestStream, InedoLib.UTF8Encoding);
                requestWriter.WriteLine("--" + boundary);
                requestWriter.WriteLine($"Content-Disposition: form-data;name=\"file\";filename=\"{PathEx.GetFileName(fileName)}\"");
                requestWriter.WriteLine("Content-Type: application/octet-stream");
                requestWriter.WriteLine();
                requestWriter.Flush();

                await sourceStream.CopyToAsync(requestStream, 65536, cancellationToken);

                requestWriter.WriteLine("\r\n--" + boundary + "--");
                requestWriter.Flush();
            }

            try
            {
                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                using (var responseStream = response.GetResponseStream())
                {
                    var buffer = new byte[16384];
                    int length = await responseStream.ReadAsync(buffer, 0, buffer.Length);

                    if (length == 0)
                    {
                        this.LogDebug("Response body is empty.");
                    }
                    else
                    {
                        try
                        {
                            this.LogInformation(InedoLib.UTF8Encoding.GetString(buffer, 0, length));
                        }
                        catch
                        {
                            this.LogWarning($"The response could not be parsed as a string; responded with {length} bytes of binary data.");
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                this.ProcessResponse((HttpWebResponse)ex.Response);
            }
        }
    }
}
