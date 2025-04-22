namespace ArquivoMate2.Domain.Document
{
    public record DocumentProcessed(Guid AggregateId, DateTime OccurredOn) : IDomainEvent;

}
