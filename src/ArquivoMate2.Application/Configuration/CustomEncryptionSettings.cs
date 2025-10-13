namespace ArquivoMate2.Application.Configuration
{
    public class CustomEncryptionSettings
    {
        public bool Enabled { get; set; }
        public string MasterKeyBase64 { get; set; } = string.Empty; // 32 bytes base64
        public int TokenTtlMinutes { get; set; } = 5;
        public int CacheTtlMinutes { get; set; } = 30;
    }
}
