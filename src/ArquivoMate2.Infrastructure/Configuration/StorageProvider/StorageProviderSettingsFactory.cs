using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Configuration.StorageProvider
{
    public class StorageProviderSettingsFactory
    {
        private readonly IConfiguration _configuration;

        public StorageProviderSettingsFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public StorageProviderSettings GetsStorageProviderSettings()
        {
            var section = _configuration.GetSection("FileProvider");
            var type = section.GetValue<StorageProviderType?>("Type")
                       ?? throw new InvalidOperationException("FileProvider:Type ist nicht konfiguriert.");

            return type switch
            {
                //FileProviderType.Local => section.Get<LocalFileProviderSettings>()
                //                               ?? throw new InvalidOperationException("LocalFileProviderSettings fehlt."),
                //FileProviderType.AzureBlob => section.Get<AzureBlobFileProviderSettings>()
                //                               ?? throw new InvalidOperationException("AzureBlobFileProviderSettings fehlt."),
                StorageProviderType.S3 => section.Get<S3StorageProviderSettings>()
                                               ?? throw new InvalidOperationException("S3FileProviderSettings fehlt."),
                //FileProviderType.NFS => section.Get<NfsFileProviderSettings>()
                //                               ?? throw new InvalidOperationException("NfsFileProviderSettings fehlt."),
                //FileProviderType.Bunny => section.Get<BunnyFileProviderSettings>()
                //                               ?? throw new InvalidOperationException("BunnyFileProviderSettings fehlt."),
                _ => throw new InvalidOperationException($"Unbekannter FileProvider-Typ: {type}")
            };
        }
    }
}
