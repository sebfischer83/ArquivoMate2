namespace ArquivoMate2.Domain.Document
{
    public record DocumentTitleSuggested(Guid AggregateId, string Title, DateTime OccurredOn) : IDomainEvent;
}
