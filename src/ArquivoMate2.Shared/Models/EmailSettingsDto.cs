using System;

namespace ArquivoMate2.Shared.Models
{
    public class EmailSettingsDto
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public EmailProviderType ProviderType { get; set; }
        public string Server { get; set; } = string.Empty;
        public int Port { get; set; }
        public bool UseSsl { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int ConnectionTimeout { get; set; }
        public string DefaultFolder { get; set; } = "INBOX";
        public bool AutoReconnect { get; set; }
    }
}
