using System;

namespace ArquivoMate2.Infrastructure.Configuration.IngestionProvider
{
    /// <summary>
    /// Configuration values for the filesystem based ingestion provider.
    /// </summary>
    public class FileSystemIngestionProviderSettings : IngestionProviderSettings
    {
        /// <summary>
        /// Root folder that contains the user specific ingestion directories.
        /// </summary>
        public string RootPath { get; set; } = string.Empty;

        /// <summary>
        /// Name of the subfolder that contains files currently being processed.
        /// </summary>
        public string ProcessingSubfolderName { get; set; } = "processing";

        /// <summary>
        /// Name of the subfolder that stores successfully processed files.
        /// </summary>
        public string ProcessedSubfolderName { get; set; } = "processed";

        /// <summary>
        /// Name of the subfolder that stores files that failed ingestion.
        /// </summary>
        public string FailedSubfolderName { get; set; } = "failed";

        /// <summary>
        /// Interval at which the Hangfire job should poll for new files.
        /// </summary>
        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(5);
    }
}
