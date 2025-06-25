namespace ArquivoMate2.Domain.Import
{
    public record MarkSuccededDocumentImport(Guid AggregateId, Guid DocumentId, DateTime OccurredOn) : IDomainEvent;
}
