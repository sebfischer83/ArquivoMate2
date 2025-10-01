namespace ArquivoMate2.Application.Configuration
{
    public class AppSettings
    {
        /// <summary>
        /// Optional public base URL (e.g. https://app.example.com). If empty, runtime Request.Scheme + Request.Host is used.
        /// </summary>
        public string? PublicBaseUrl { get; set; }

        /// <summary>
        /// Default TTL in minutes for public shares when client does not specify.
        /// </summary>
        public int PublicShareDefaultTtlMinutes { get; set; } = 60;

        /// <summary>
        /// Maximum allowed TTL in minutes for public shares.
        /// </summary>
        public int PublicShareMaxTtlMinutes { get; set; } = 1440; // 24h
    }
}
