namespace ArquivoMate2.Domain.Document
{
    public record DocumentUpdated(Guid AggregateId, Dictionary<string, object> Values, DateTime OccurredOn) : IDomainEvent;

}
