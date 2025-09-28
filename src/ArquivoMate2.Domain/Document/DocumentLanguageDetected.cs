namespace ArquivoMate2.Domain.Document
{
    public record DocumentLanguageDetected(Guid AggregateId, string IsoCode, DateTime OccurredOn) : IDomainEvent;
}
