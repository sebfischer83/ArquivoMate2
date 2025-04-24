namespace ArquivoMate2.Domain.Document
{
    public record DocumentUploaded(Guid AggregateId, string UserId, string FilePath, DateTime OccurredOn) : IDomainEvent;

}
