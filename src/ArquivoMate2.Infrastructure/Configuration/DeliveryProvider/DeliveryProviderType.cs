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
        Cloudfront
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
                DeliveryProviderType.S3 => section.GetSection("Args").Get<S3DeliveryProviderSettings>()
                                        ?? throw new InvalidOperationException("S3DeliveryProviderSettings fehlt."),
                DeliveryProviderType.Bunny => section.GetSection("Args").Get<BunnyDeliveryProviderSettings>()
                                        ?? throw new InvalidOperationException("BunnyDeliveryProviderSettings fehlt."),
                _ => throw new InvalidOperationException($"Unbekannter DeliveryProvider-Typ: {type}")
            };
        }
    }
}
