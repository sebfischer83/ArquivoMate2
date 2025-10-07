using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Configuration.StorageProvider
{
    public class StorageProviderSettingsFactory
    {
        private readonly IConfiguration _configuration;

        public StorageProviderSettingsFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public StorageProviderSettings GetsStorageProviderSettings()
        {
            var section = _configuration.GetSection("StorageProvider");
            var type = section.GetValue<StorageProviderType?>("Type")
                       ?? throw new InvalidOperationException("FileProvider:Type ist nicht konfiguriert.");

            return type switch
            {
                StorageProviderType.S3 => BuildS3Settings(section),
                _ => throw new InvalidOperationException($"Unbekannter FileProvider-Typ: {type}")
            };
        }

        private S3StorageProviderSettings BuildS3Settings(IConfigurationSection section)
        {
            var argsSection = section.GetSection("Args");
            // Bind S3 settings from Args (most settings live here)
            var s3 = argsSection.Get<S3StorageProviderSettings>() ?? new S3StorageProviderSettings();

            // Always take Type and RootPath from the parent StorageProvider section
            var parentType = section.GetValue<StorageProviderType?>("Type");
            if (parentType.HasValue)
            {
                s3.Type = parentType.Value;
            }

            var parentRoot = section.GetValue<string>("RootPath");
            if (!string.IsNullOrWhiteSpace(parentRoot))
            {
                s3.RootPath = parentRoot;
            }

            return s3;
        }
    }
}
