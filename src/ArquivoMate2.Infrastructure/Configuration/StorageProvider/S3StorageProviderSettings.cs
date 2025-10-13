namespace ArquivoMate2.Infrastructure.Configuration.StorageProvider
{
    public class S3StorageProviderSettings : StorageProviderSettings
    {
        public string AccessKey { get; set; } = string.Empty;

        public string SecretKey { get; set; } = string.Empty;

        public string Endpoint { get; set; } = string.Empty;

        public string BucketName { get; set; } = string.Empty;

        public string Region { get; set; } = string.Empty;

        public bool IsPublic { get; set; } = false;

        /// <summary>
        /// Optional SSE-C (Server-Side Encryption with Customer-Provided Keys) configuration.
        /// When enabled, all S3 operations will include SSE-C headers.
        /// </summary>
        public SseCConfiguration? SseC { get; set; }
    }
}
