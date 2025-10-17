namespace ArquivoMate2.Domain.Document
{
    public record DocumentFilesPrepared(Guid AggregateId, string FilePath, string MetadataPath, string ThumbnailPath, string PreviewPath, string ArchivePath, string OriginalFileName, DateTime OccurredOn) : IDomainEvent;

}
