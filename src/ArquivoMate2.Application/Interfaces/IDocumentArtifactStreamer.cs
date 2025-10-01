namespace ArquivoMate2.Application.Interfaces
{
    public interface IDocumentArtifactStreamer
    {
        Task<(byte[] Content, string ContentType)> GetAsync(Guid documentId, string artifact, CancellationToken ct);
    }
}
