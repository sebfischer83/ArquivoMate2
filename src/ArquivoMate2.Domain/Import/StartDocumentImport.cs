namespace ArquivoMate2.Domain.Import
{
    public record StartDocumentImport(Guid AggregateId, DateTime OccurredOn) : IDomainEvent;
}
