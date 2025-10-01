namespace ArquivoMate2.Domain.Document
{
    public record EncryptedArtifactKey(string Artifact, byte[] WrappedDek, byte[] WrapNonce, string Algorithm, string Version);
}
