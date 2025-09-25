namespace ArquivoMate2.Domain.Document
{
    public record DocumentTitleInitialized(Guid AggregateId, string Title, DateTime OccurredOn) : IDomainEvent;
}
