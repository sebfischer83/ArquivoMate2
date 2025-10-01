namespace ArquivoMate2.Application.Interfaces
{
    public interface IFileAccessTokenService
    {
        string Create(Guid documentId, string artifact, DateTimeOffset expiresAt);
        bool TryValidate(string token, out Guid documentId, out string artifact);

        // Externe Shares
        string CreateShareToken(Guid shareId, DateTimeOffset expiresAt);
        bool TryValidateShareToken(string token, out Guid shareId, out DateTimeOffset expiresAt);
    }
}
