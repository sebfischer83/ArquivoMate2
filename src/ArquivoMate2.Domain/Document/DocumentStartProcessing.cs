namespace ArquivoMate2.Domain.Document
{
    public record DocumentStartProcessing(Guid AggregateId, DateTime OccurredOn) : IDomainEvent;

}
