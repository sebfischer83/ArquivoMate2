using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Infrastructure.Configuration.DeliveryProvider;
using ArquivoMate2.Infrastructure.Configuration.IngestionProvider;
using ArquivoMate2.Infrastructure.Configuration.StorageProvider;
using ArquivoMate2.Shared.Models;

namespace ArquivoMate2.Infrastructure.Services.Encryption
{
    /// <summary>
    /// Derives the default encryption mode that applies to stored documents
    /// when no per-document custom encryption is present.
    /// </summary>
    public class DocumentEncryptionDescriptor : IDocumentEncryptionDescriptor
    {
        public DocumentEncryptionDescriptor(
            StorageProviderSettings storage,
            DeliveryProviderSettings delivery,
            IngestionProviderSettings ingestion)
        {
            DefaultEncryption = DetermineDefault(storage, delivery, ingestion);
        }

        public DocumentEncryptionType DefaultEncryption { get; }

        private static DocumentEncryptionType DetermineDefault(
            StorageProviderSettings storage,
            DeliveryProviderSettings delivery,
            IngestionProviderSettings ingestion)
        {
            if (storage is S3StorageProviderSettings s3Storage &&
                s3Storage.CustomerEncryption?.Enabled == true)
            {
                return DocumentEncryptionType.S3;
            }

            if (delivery is S3DeliveryProviderSettings s3Delivery &&
                s3Delivery.CustomerEncryption?.Enabled == true)
            {
                return DocumentEncryptionType.S3;
            }

            if (ingestion is S3IngestionProviderSettings s3Ingestion &&
                s3Ingestion.CustomerEncryption?.Enabled == true)
            {
                return DocumentEncryptionType.S3;
            }

            return DocumentEncryptionType.Unencrypted;
        }
    }
}
