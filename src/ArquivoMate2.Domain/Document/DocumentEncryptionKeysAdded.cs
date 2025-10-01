namespace ArquivoMate2.Domain.Document
{
    public record DocumentEncryptionKeysAdded(Guid AggregateId, IReadOnlyCollection<EncryptedArtifactKey> Artifacts, DateTime OccurredOn) : IDomainEvent;
}
