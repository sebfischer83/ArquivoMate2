using System;

namespace ArquivoMate2.Infrastructure.Configuration.IngestionProvider
{
    /// <summary>
    /// Configuration values for the S3 based ingestion provider.
    /// </summary>
    public class S3IngestionProviderSettings : IngestionProviderSettings
    {
        public string AccessKey { get; set; } = string.Empty;

        public string SecretKey { get; set; } = string.Empty;

        public string Endpoint { get; set; } = string.Empty;

        public string BucketName { get; set; } = string.Empty;

        public string Region { get; set; } = string.Empty;

        public bool UseSsl { get; set; } = true;

        /// <summary>
        /// Root prefix under which the user specific directories reside.
        /// </summary>
        public string RootPrefix { get; set; } = "ingestion";

        public string ProcessingSubfolderName { get; set; } = "processing";

        public string ProcessedSubfolderName { get; set; } = "processed";

        public string FailedSubfolderName { get; set; } = "failed";

        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(5);
    }
}
