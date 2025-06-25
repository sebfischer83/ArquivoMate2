namespace ArquivoMate2.Domain.Import
{
    public record MarkFailedDocumentImport(Guid AggregateId, string ErrorMessage, DateTime OccurredOn) : IDomainEvent;
}
