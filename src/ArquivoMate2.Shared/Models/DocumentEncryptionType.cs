using System;
using System.Text.Json.Serialization;

namespace ArquivoMate2.Shared.Models
{
    /// <summary>
    /// Defines the encryption method(s) used for document storage.
    /// Can be combined using bitwise flags for multiple encryption layers.
    /// </summary>
    [Flags]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DocumentEncryptionType
    {
        /// <summary>
        /// No encryption is applied.
        /// </summary>
        None = 0,

        /// <summary>
        /// Client-side encryption using application-managed keys (DEK wrapped by master key).
        /// Applied at application layer before storage.
        /// </summary>
        ClientSide = 1 << 0,  // 1

        /// <summary>
        /// Server-Side Encryption with Customer-Provided Keys (SSE-C).
        /// S3-compatible storage encrypts the object using a key provided in the request.
        /// Applied at storage layer.
        /// </summary>
        SseC = 1 << 1  // 2
        
        // Future encryption types can be added:
        // SseS3 = 1 << 2,  // 4 - S3-managed server-side encryption
        // SseKms = 1 << 3, // 8 - KMS-managed server-side encryption
    }
}
