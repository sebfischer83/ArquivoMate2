using System.Text.Json.Serialization;

namespace ArquivoMate2.Shared.Models
{
    /// <summary>
    /// Represents the encryption mode applied to a document artifact.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DocumentEncryptionType
    {
        /// <summary>
        /// No additional encryption layer is applied by the platform.
        /// </summary>
        Unencrypted = 0,

        /// <summary>
        /// The artifact is protected by S3 server-side encryption with
        /// customer provided keys (SSE-C).
        /// </summary>
        S3 = 1,

        /// <summary>
        /// The artifact is encrypted by the custom application-managed
        /// encryption pipeline.
        /// </summary>
        Custom = 2
    }
}
