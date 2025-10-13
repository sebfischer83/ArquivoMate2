using System;

namespace ArquivoMate2.Infrastructure.Configuration.StorageProvider
{
    /// <summary>
    /// Configuration for Server-Side Encryption with Customer-Provided Keys (SSE-C).
    /// </summary>
    public class SseCConfiguration
    {
        /// <summary>
        /// Indicates whether SSE-C encryption should be used for S3 operations.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Base64-encoded customer-provided encryption key (must be 256-bit / 32 bytes).
        /// This key is sent with each S3 request for encryption/decryption.
        /// </summary>
        public string CustomerKeyBase64 { get; set; } = string.Empty;

        /// <summary>
        /// Validates the SSE-C configuration.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when configuration is invalid.</exception>
        public void Validate()
        {
            if (!Enabled) return;

            if (string.IsNullOrWhiteSpace(CustomerKeyBase64))
            {
                throw new InvalidOperationException("SSE-C is enabled but CustomerKeyBase64 is not configured.");
            }

            try
            {
                var key = Convert.FromBase64String(CustomerKeyBase64);
                if (key.Length != 32)
                {
                    throw new InvalidOperationException("SSE-C CustomerKeyBase64 must be a 256-bit (32 byte) key.");
                }
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("SSE-C CustomerKeyBase64 is not a valid Base64 string.");
            }
        }
    }
}
