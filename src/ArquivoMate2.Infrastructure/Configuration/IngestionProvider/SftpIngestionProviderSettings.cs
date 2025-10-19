using System;

namespace ArquivoMate2.Infrastructure.Configuration.IngestionProvider
{
    /// <summary>
    /// Configuration values for the SFTP based ingestion provider.
    /// </summary>
    public class SftpIngestionProviderSettings : IngestionProviderSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 22;
        public string Username { get; set; } = string.Empty;
        public string? Password { get; set; }
        public string? PrivateKeyFilePath { get; set; }
        public string? PrivateKeyPassphrase { get; set; }

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
