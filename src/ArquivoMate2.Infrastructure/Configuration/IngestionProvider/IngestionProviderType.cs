namespace ArquivoMate2.Infrastructure.Configuration.IngestionProvider
{
    /// <summary>
    /// Known ingestion provider types supported by the application.
    /// </summary>
    public enum IngestionProviderType
    {
        FileSystem,
        S3,
        Sftp,
        None
    }
}
