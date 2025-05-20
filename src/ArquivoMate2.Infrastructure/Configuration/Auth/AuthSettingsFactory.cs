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
                AuthType.OIDC => section.GetSection("Args").Get<OIDCSettings>()
                                               ?? throw new InvalidOperationException("OIDC fehlt."),
                _ => throw new InvalidOperationException($"Unbekannter ChatBot-Typ: {type}")
            };
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
        public string Issuer { get; set; }
    }

    public enum AuthType
    {
        OIDC
    }
}
