namespace ArquivoMate2.Infrastructure.Configuration.StorageProvider
{
    public class S3StorageProviderSettings : StorageProviderSettings
    {
        public string AccessKey { get; set; } = string.Empty;

        public string SecretKey { get; set; } = string.Empty;

        public string Endpoint { get; set; } = string.Empty;

        public string BucketName { get; set; } = string.Empty;

        public string Region { get; set; } = string.Empty;
    }
}
