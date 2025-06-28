namespace ArquivoMate2.Domain.Import
{
    public record HideDocumentImport(Guid AggregateId, DateTime OccurredOn) : IDomainEvent;
}
