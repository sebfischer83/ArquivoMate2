namespace ArquivoMate2.Domain.Document
{
    public record DocumentFilesPrepared(Guid AggregateId, string FilePath, string MetadataPath, string ThumbnailPath, string PreviewPath, DateTime OccurredOn) : IDomainEvent;

}
