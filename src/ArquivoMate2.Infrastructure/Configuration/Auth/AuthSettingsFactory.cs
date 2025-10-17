using ArquivoMate2.Infrastructure.Configuration.Llm;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Configuration.Auth
{
    public class AuthSettingsFactory
    {
        private readonly IConfiguration _configuration;

        public AuthSettingsFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public AuthSettings GetAuthSettings()
        {
            var section = _configuration.GetSection("Auth");
            var type = section.GetValue<AuthType?>("Type")
                       ?? throw new InvalidOperationException("Auth:Type ist nicht konfiguriert.");

            return type switch
            {
                AuthType.OIDC => BindAndValidateOidc(section),
                _ => throw new InvalidOperationException($"Unbekannter ChatBot-Typ: {type}")
            };
        }

        private OIDCSettings BindAndValidateOidc(IConfigurationSection section)
        {
            var settings = section.GetSection("Args").Get<OIDCSettings>()
                           ?? throw new InvalidOperationException("OIDC fehlt.");

            // Validate required OIDC properties and fail fast with a helpful message listing missing fields
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(settings.Authority)) missing.Add("Auth:Args:Authority");
            if (string.IsNullOrWhiteSpace(settings.Audience)) missing.Add("Auth:Args:Audience");
            if (string.IsNullOrWhiteSpace(settings.Issuer)) missing.Add("Auth:Args:Issuer");
            if (string.IsNullOrWhiteSpace(settings.ClientId)) missing.Add("Auth:Args:ClientId");

            if (missing.Any())
            {
                throw new ArgumentException($"OIDC configuration is missing required properties: {string.Join(", ", missing)}.");
            }

            // Ensure the returned settings reports the type so callers can inspect it reliably
            settings.Type = AuthType.OIDC;

            return settings;
        }
    }

    public class  AuthSettings
    {
        public AuthType Type { get; set; }
    }

    public class OIDCSettings : AuthSettings
    {
        public string ClientId { get; set; } = string.Empty;

        public string Audience { get; set; } = string.Empty;

        public string Authority { get; set; } = string.Empty;
        public required string Issuer { get; set; }

        public string? CookieDomain { get; set; }
    }

    public enum AuthType
    {
        OIDC
    }
}
