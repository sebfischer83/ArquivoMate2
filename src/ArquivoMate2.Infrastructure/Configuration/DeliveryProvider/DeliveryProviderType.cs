using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Configuration.DeliveryProvider
{
    public enum DeliveryProviderType
    {
        Noop,
        S3,
        Bunny,
        Cloudfront,
        Server // New: route delivery through the API server (/api/delivery/...)
    }

    public class DeliveryProviderSettings
    {
        public DeliveryProviderType Type { get; set; }
    }

    public class S3DeliveryProviderSettings : DeliveryProviderSettings
    {
        public string AccessKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public string BucketName { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public bool IsPublic { get; set; } = false;

        /// <summary>
        /// Optional SSE-C (Server-Side Encryption with Customer-Provided Keys) configuration.
        /// When enabled, presigned URLs cannot be used. Delivery must go through the server.
        /// </summary>
        public StorageProvider.SseCConfiguration? SseC { get; set; }
    }

    public class BunnyDeliveryProviderSettings : DeliveryProviderSettings
    {
        public string TokenAuthenticationKey { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;

        public bool UseTokenAuthentication { get; set; } = false;

        public bool UseTokenIpValidation { get; set; } = false;

        public bool UseTokenPath { get; set; } = false;

        public string TokenCountries { get; set; } = string.Empty;

        public string TokenCountriesBlocked { get; set; } = string.Empty;


        public override string ToString()
        {
            return $"{Host}{UseTokenAuthentication}{UseTokenIpValidation}";
        }
    }

    public class DeliveryProviderSettingsFactory
    {
        private readonly IConfiguration _configuration;

        public DeliveryProviderSettingsFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public DeliveryProviderSettings GetDeliveryProviderSettings()
        {
            var section = _configuration.GetSection("DeliveryProvider");
            var type = section.GetValue<DeliveryProviderType?>("Type")
                       ?? throw new InvalidOperationException("DeliveryProvider:Type ist nicht konfiguriert.");

            return type switch
            {
                DeliveryProviderType.Noop => new DeliveryProviderSettings { Type = DeliveryProviderType.Noop },
                DeliveryProviderType.S3 => BindAndMarkS3(section),
                DeliveryProviderType.Bunny => BindAndMarkBunny(section),
                DeliveryProviderType.Server => new DeliveryProviderSettings { Type = DeliveryProviderType.Server },
                _ => throw new InvalidOperationException($"Unbekannter DeliveryProvider-Typ: {type}")
            };
        }

        private static S3DeliveryProviderSettings BindAndMarkS3(IConfigurationSection section)
        {
            var s3 = section.GetSection("Args").Get<S3DeliveryProviderSettings>()
                     ?? throw new InvalidOperationException("S3DeliveryProviderSettings fehlt.");
            s3.Type = DeliveryProviderType.S3; // Variante A: Type nach Binding setzen
            return s3;
        }

        private static BunnyDeliveryProviderSettings BindAndMarkBunny(IConfigurationSection section)
        {
            var bunny = section.GetSection("Args").Get<BunnyDeliveryProviderSettings>()
                        ?? throw new InvalidOperationException("BunnyDeliveryProviderSettings fehlt.");
            bunny.Type = DeliveryProviderType.Bunny; // Variante A: Type nach Binding setzen
            return bunny;
        }
    }
}
