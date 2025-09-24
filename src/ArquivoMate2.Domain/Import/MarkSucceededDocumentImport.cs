namespace ArquivoMate2.Domain.Import
{
    public record MarkSucceededDocumentImport(Guid AggregateId, Guid DocumentId, DateTime OccurredOn) : IDomainEvent;
}
