namespace ArquivoMate2.Infrastructure.Configuration.StorageProvider
{
    public class StorageProviderSettings
    {
        public StorageProviderType Type { get; set; }

        public required string RootPath { get; set; }
    }
}
