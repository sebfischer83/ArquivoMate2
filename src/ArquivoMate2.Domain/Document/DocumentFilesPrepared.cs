namespace ArquivoMate2.Domain.Document
{
    public record DocumentFilesPrepared(Guid AggregateId, string FilePath, string MetadataPath, string ThumbnailPath, DateTime OccurredOn) : IDomainEvent;

    public record DocumentStartProcessing(Guid AggregateId, DateTime OccurredOn) : IDomainEvent;

}
