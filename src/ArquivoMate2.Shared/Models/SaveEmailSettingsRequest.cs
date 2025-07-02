using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ArquivoMate2.Shared.Models
{
    public class SaveEmailSettingsRequest
    {
        [Required]
        [JsonPropertyName("providerType")]
        public EmailProviderType ProviderType { get; set; }

        [Required]
        [JsonPropertyName("server")]
        public string Server { get; set; } = string.Empty;

        [Required]
        [Range(1, 65535)]
        [JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonPropertyName("useSsl")]
        public bool UseSsl { get; set; } = true;

        [Required]
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("connectionTimeout")]
        public int? ConnectionTimeout { get; set; }

        [JsonPropertyName("defaultFolder")]
        public string? DefaultFolder { get; set; }

        [JsonPropertyName("autoReconnect")]
        public bool? AutoReconnect { get; set; }
    }
}
