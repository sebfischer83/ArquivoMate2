namespace ArquivoMate2.Domain.Import
{
    public record InitDocumentImport(Guid AggregateId, string UserId, string FileName, DateTime OccurredOn, ImportSource Source = ImportSource.User) : IDomainEvent;
}
