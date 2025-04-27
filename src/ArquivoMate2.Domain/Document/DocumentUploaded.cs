namespace ArquivoMate2.Domain.Document
{
    public record DocumentUploaded(Guid AggregateId, string UserId, DateTime OccurredOn) : IDomainEvent;

}
