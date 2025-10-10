namespace ArquivoMate2.Infrastructure.Configuration.IngestionProvider
{
    /// <summary>
    /// Base settings for ingestion providers. Specialized providers can extend this
    /// type with additional configuration values.
    /// </summary>
    public class IngestionProviderSettings
    {
        public IngestionProviderType Type { get; set; } = IngestionProviderType.None;

        /// <summary>
        /// Optional email address that should be used as the sender when creating
        /// EmailDocument instances from ingestion sources. If null or empty, no
        /// sender address will be provided.
        /// </summary>
        public string IngestionEmail { get; set; } = string.Empty;
    }
}
