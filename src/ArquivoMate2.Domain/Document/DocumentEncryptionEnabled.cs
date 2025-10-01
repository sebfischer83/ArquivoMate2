namespace ArquivoMate2.Domain.Document
{
    public record DocumentEncryptionEnabled(Guid AggregateId, DateTime OccurredOn) : IDomainEvent;
}
