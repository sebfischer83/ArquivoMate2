namespace ArquivoMate2.Domain.Document
{
    public record DocumentEncryptionTypeSet(Guid AggregateId, int EncryptionType, DateTime OccurredOn) : IDomainEvent;
}
