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

        /// <summary>
        /// Initializes a new IngestionProviderSettingsFactory that uses the provided configuration to resolve ingestion provider settings.
        /// </summary>
        /// <param name="configuration">Application configuration root used to read the "IngestionProvider" section and its nested settings.</param>
        public IngestionProviderSettingsFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Resolves and returns ingestion provider settings from the application's configuration.
        /// </summary>
        /// <returns>
        /// An <see cref="IngestionProviderSettings"/> populated for the configured provider.
        /// If the "IngestionProvider" section is missing or its "Type" is not set, the returned settings have <see cref="IngestionProviderType.None"/>.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown when the configuration specifies an unsupported <see cref="IngestionProviderType"/> value.</exception>
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
                IngestionProviderType.None => new IngestionProviderSettings { Type = IngestionProviderType.None },
                _ => throw new InvalidOperationException($"Unsupported IngestionProvider type: {type}")
            };
        }

        /// <summary>
        /// Constructs and validates FileSystem ingestion provider settings from the given configuration section.
        /// </summary>
        /// <param name="section">Configuration section containing the FileSystem provider settings (expects an optional "Args" subsection and/or a top-level "RootPath").</param>
        /// <returns>A FileSystemIngestionProviderSettings instance populated from configuration with defaults applied.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the RootPath is not configured in either "Args" or the section's "RootPath".</exception>
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

        /// <summary>
        /// Builds and validates S3 ingestion provider settings from the given configuration section.
        /// </summary>
        /// <param name="section">Configuration section containing an "Args" subsection with S3 settings.</param>
        /// <returns>A configured <see cref="S3IngestionProviderSettings"/> with required fields validated and defaults applied for optional values.</returns>
        /// <exception cref="InvalidOperationException">Thrown when any of the required settings `BucketName`, `AccessKey`, `SecretKey`, or `Endpoint` is missing or empty in the configuration.</exception>
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
    }
}