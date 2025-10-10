namespace ArquivoMate2.Infrastructure.Configuration.IngestionProvider
{
    /// <summary>
    /// Base settings for ingestion providers. Specialized providers can extend this
    /// type with additional configuration values.
    /// </summary>
    public class IngestionProviderSettings
    {
        public IngestionProviderType Type { get; set; } = IngestionProviderType.None;
    }
}
