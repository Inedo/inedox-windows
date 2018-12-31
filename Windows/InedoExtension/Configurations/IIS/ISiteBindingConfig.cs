using System.Security.Cryptography.X509Certificates;
using Inedo.Extensibility.Configurations;

namespace Inedo.Extensions.Windows.Configurations.IIS
{
    internal interface ISiteBindingConfig : IExistential
    {
        string SiteName { get; set; }
        string Protocol { get; set; }
        string Address { get; set; }
        string HostName { get; set; }
        int Port { get; set; }
        string SslCertificateName { get; set; }
        StoreLocation SslStoreLocation { get; set; }
        string SslCertificateHash { get; set; }
        bool RequireServerNameIndication { get; set; }
        string SslCertificateStore { get; set; }
        bool IsFullyPopulated { get; set; }
    }
}
