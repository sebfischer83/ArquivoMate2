using Microsoft.Extensions.Configuration;
using System;

namespace ArquivoMate2.Infrastructure.Configuration.IngestionProvider
{
    /// <summary>
    /// Resolves the configured ingestion provider settings from configuration.
    /// </summary>
    public class IngestionProviderSettingsFactory
    {
        private readonly IConfiguration _configuration;

        public IngestionProviderSettingsFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IngestionProviderSettings GetIngestionProviderSettings()
        {
            var section = _configuration.GetSection("IngestionProvider");
            if (!section.Exists())
            {
                return new IngestionProviderSettings { Type = IngestionProviderType.None };
            }

            var type = section.GetValue<IngestionProviderType?>("Type");
            if (type is null)
            {
                return new IngestionProviderSettings { Type = IngestionProviderType.None };
            }

            return type.Value switch
            {
                IngestionProviderType.FileSystem => BuildFileSystemSettings(section),
                IngestionProviderType.S3 => BuildS3Settings(section),
                IngestionProviderType.Sftp => BuildSftpSettings(section),
                IngestionProviderType.None => new IngestionProviderSettings { Type = IngestionProviderType.None },
                _ => throw new InvalidOperationException($"Unsupported IngestionProvider type: {type}")
            };
        }

        private FileSystemIngestionProviderSettings BuildFileSystemSettings(IConfigurationSection section)
        {
            var argsSection = section.GetSection("Args");
            var settings = argsSection.Get<FileSystemIngestionProviderSettings>() ?? new FileSystemIngestionProviderSettings();
            settings.Type = IngestionProviderType.FileSystem;

            if (string.IsNullOrWhiteSpace(settings.RootPath))
            {
                settings.RootPath = section.GetValue<string>("RootPath")
                    ?? throw new InvalidOperationException("IngestionProvider:Args:RootPath must be configured.");
            }

            if (settings.PollingInterval <= TimeSpan.Zero)
            {
                settings.PollingInterval = TimeSpan.FromMinutes(5);
            }

            return settings;
        }

        private S3IngestionProviderSettings BuildS3Settings(IConfigurationSection section)
        {
            var argsSection = section.GetSection("Args");
            var settings = argsSection.Get<S3IngestionProviderSettings>() ?? new S3IngestionProviderSettings();
            settings.Type = IngestionProviderType.S3;

            if (string.IsNullOrWhiteSpace(settings.BucketName))
            {
                throw new InvalidOperationException("IngestionProvider:Args:BucketName must be configured.");
            }

            if (string.IsNullOrWhiteSpace(settings.AccessKey))
            {
                throw new InvalidOperationException("IngestionProvider:Args:AccessKey must be configured.");
            }

            if (string.IsNullOrWhiteSpace(settings.SecretKey))
            {
                throw new InvalidOperationException("IngestionProvider:Args:SecretKey must be configured.");
            }

            if (string.IsNullOrWhiteSpace(settings.Endpoint))
            {
                throw new InvalidOperationException("IngestionProvider:Args:Endpoint must be configured.");
            }

            if (settings.PollingInterval <= TimeSpan.Zero)
            {
                settings.PollingInterval = TimeSpan.FromMinutes(5);
            }

            if (string.IsNullOrWhiteSpace(settings.RootPrefix))
            {
                settings.RootPrefix = "ingestion";
            }

            if (string.IsNullOrWhiteSpace(settings.ProcessingSubfolderName))
            {
                settings.ProcessingSubfolderName = "processing";
            }

            if (string.IsNullOrWhiteSpace(settings.ProcessedSubfolderName))
            {
                settings.ProcessedSubfolderName = "processed";
            }

            if (string.IsNullOrWhiteSpace(settings.FailedSubfolderName))
            {
                settings.FailedSubfolderName = "failed";
            }

            return settings;
        }

        private SftpIngestionProviderSettings BuildSftpSettings(IConfigurationSection section)
        {
            var argsSection = section.GetSection("Args");
            var settings = argsSection.Get<SftpIngestionProviderSettings>() ?? new SftpIngestionProviderSettings();
            settings.Type = IngestionProviderType.Sftp;

            if (string.IsNullOrWhiteSpace(settings.Host))
            {
                throw new InvalidOperationException("IngestionProvider:Args:Host must be configured.");
            }

            if (settings.Port <= 0)
            {
                settings.Port = 22;
            }

            if (string.IsNullOrWhiteSpace(settings.Username))
            {
                throw new InvalidOperationException("IngestionProvider:Args:Username must be configured.");
            }

            if (settings.PollingInterval <= TimeSpan.Zero)
            {
                settings.PollingInterval = TimeSpan.FromMinutes(5);
            }

            if (string.IsNullOrWhiteSpace(settings.RootPrefix))
            {
                settings.RootPrefix = "ingestion";
            }

            if (string.IsNullOrWhiteSpace(settings.ProcessingSubfolderName))
            {
                settings.ProcessingSubfolderName = "processing";
            }

            if (string.IsNullOrWhiteSpace(settings.ProcessedSubfolderName))
            {
                settings.ProcessedSubfolderName = "processed";
            }

            if (string.IsNullOrWhiteSpace(settings.FailedSubfolderName))
            {
                settings.FailedSubfolderName = "failed";
            }

            return settings;
        }
    }
}
