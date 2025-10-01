namespace ArquivoMate2.Application.Interfaces
{
    /// <summary>
    /// Generates and validates secure tokens for accessing protected document artifacts.
    /// </summary>
    public interface IFileAccessTokenService
    {
        /// <summary>
        /// Creates a signed token for the specified document artifact.
        /// </summary>
        /// <param name="documentId">Identifier of the document.</param>
        /// <param name="artifact">Artifact name (file, preview, etc.).</param>
        /// <param name="expiresAt">Expiry timestamp for the token.</param>
        /// <returns>The signed token string.</returns>
        string Create(Guid documentId, string artifact, DateTimeOffset expiresAt);

        /// <summary>
        /// Validates a delivery token and extracts its payload.
        /// </summary>
        /// <param name="token">Token to validate.</param>
        /// <param name="documentId">Document identifier extracted from the token.</param>
        /// <param name="artifact">Artifact name extracted from the token.</param>
        /// <returns><c>true</c> when the token is valid; otherwise, <c>false</c>.</returns>
        bool TryValidate(string token, out Guid documentId, out string artifact);

        // Tokens for external sharing links
        /// <summary>
        /// Creates a token that secures an external share link.
        /// </summary>
        /// <param name="shareId">Identifier of the share.</param>
        /// <param name="expiresAt">Expiry timestamp for the token.</param>
        /// <returns>The signed share token.</returns>
        string CreateShareToken(Guid shareId, DateTimeOffset expiresAt);

        /// <summary>
        /// Validates an external share token and returns its payload when successful.
        /// </summary>
        /// <param name="token">Token to validate.</param>
        /// <param name="shareId">Share identifier extracted from the token.</param>
        /// <param name="expiresAt">Expiry timestamp extracted from the token.</param>
        /// <returns><c>true</c> when the token is valid; otherwise, <c>false</c>.</returns>
        bool TryValidateShareToken(string token, out Guid shareId, out DateTimeOffset expiresAt);
    }
}
