namespace ArquivoMate2.Domain.Document
{
    // Raised when a document is logically deleted (soft delete)
    public record DocumentDeleted(Guid AggregateId, DateTime OccurredOn) : IDomainEvent;
}
