using ArquivoMate2.Shared.Models;

namespace ArquivoMate2.Application.Extensions
{
    /// <summary>
    /// Extension methods for working with DocumentEncryptionType flags.
    /// </summary>
    public static class DocumentEncryptionTypeExtensions
    {
        /// <summary>
        /// Checks if a specific encryption type is enabled.
        /// </summary>
        public static bool HasFlag(this DocumentEncryptionType type, DocumentEncryptionType flag)
        {
            return (type & flag) == flag;
        }

        /// <summary>
        /// Adds an encryption type to the current flags.
        /// </summary>
        public static DocumentEncryptionType AddFlag(this DocumentEncryptionType type, DocumentEncryptionType flag)
        {
            return type | flag;
        }

        /// <summary>
        /// Removes an encryption type from the current flags.
        /// </summary>
        public static DocumentEncryptionType RemoveFlag(this DocumentEncryptionType type, DocumentEncryptionType flag)
        {
            return type & ~flag;
        }

        /// <summary>
        /// Checks if client-side encryption is enabled.
        /// </summary>
        public static bool HasClientSideEncryption(this DocumentEncryptionType type)
        {
            return type.HasFlag(DocumentEncryptionType.ClientSide);
        }

        /// <summary>
        /// Checks if SSE-C encryption is enabled.
        /// </summary>
        public static bool HasSseCEncryption(this DocumentEncryptionType type)
        {
            return type.HasFlag(DocumentEncryptionType.SseC);
        }

        /// <summary>
        /// Checks if any encryption is enabled.
        /// </summary>
        public static bool IsEncrypted(this DocumentEncryptionType type)
        {
            return type != DocumentEncryptionType.None;
        }
    }
}
