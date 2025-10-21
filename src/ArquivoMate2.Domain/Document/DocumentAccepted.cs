namespace ArquivoMate2.Domain.Document
{
    public record DocumentAccepted(Guid AggregateId, bool Accepted, DateTime OccurredOn) : IDomainEvent;
}
