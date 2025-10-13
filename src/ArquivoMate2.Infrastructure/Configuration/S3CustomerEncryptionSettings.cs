using System;
using Minio.DataModel.Encryption;

namespace ArquivoMate2.Infrastructure.Configuration
{
    /// <summary>
    /// Configuration for S3 server-side encryption with customer provided keys (SSE-C).
    /// </summary>
    public class S3CustomerEncryptionSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether SSE-C should be used for S3 operations.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the base64 encoded customer provided key used for SSE-C operations.
        /// Must decode to 32 bytes (AES-256).
        /// </summary>
        public string CustomerKeyBase64 { get; set; } = string.Empty;

        /// <summary>
        /// Creates an <see cref="SseCustomerKey"/> instance when SSE-C is enabled and configured.
        /// Returns <c>null</c> when the feature is disabled.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when SSE-C is enabled but the key is missing or invalid.</exception>
        public SseCustomerKey? CreateCustomerKey()
        {
            if (!Enabled)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(CustomerKeyBase64))
            {
                throw new InvalidOperationException("S3 SSE-C encryption is enabled but no customer key was provided.");
            }

            try
            {
                var keyBytes = Convert.FromBase64String(CustomerKeyBase64);
                if (keyBytes.Length != 32)
                {
                    throw new InvalidOperationException("S3 SSE-C customer key must decode to 32 bytes (256 bit).");
                }

                return new SseCustomerKey(keyBytes);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("S3 SSE-C customer key is not a valid base64 string.", ex);
            }
        }
    }
}
