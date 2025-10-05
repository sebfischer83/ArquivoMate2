namespace ArquivoMate2.Infrastructure.Configuration.StorageProvider
{
    public class StorageProviderSettings
    {
        public StorageProviderType Type { get; set; }

        // Base root path used by storage providers when constructing object keys/paths.
        // Default kept for backward compatibility but can be overridden via configuration.
        public string RootPath { get; set; } = "arquivomate";
    }
}
