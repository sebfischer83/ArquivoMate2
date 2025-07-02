using ArquivoMate2.Shared.Models;
using System;

namespace ArquivoMate2.Domain.Email
{
    public class EmailSettings
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public EmailProviderType ProviderType { get; set; }
        public string Server { get; set; } = string.Empty;
        public int Port { get; set; }
        public bool UseSsl { get; set; } = true;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Advanced settings
        public int ConnectionTimeout { get; set; } = 30000; // 30 seconds
        public string DefaultFolder { get; set; } = "INBOX";
        public bool AutoReconnect { get; set; } = true;
    }
}