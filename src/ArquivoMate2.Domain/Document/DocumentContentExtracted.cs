namespace ArquivoMate2.Domain.Document
{
    public record DocumentContentExtracted(Guid AggregateId, string Content, DateTime OccurredOn) : IDomainEvent;

}
