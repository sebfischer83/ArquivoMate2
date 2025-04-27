namespace ArquivoMate2.Domain.Document
{

    public class Document
    {
        public Guid Id { get; private set; }
        public string FilePath { get; private set; } = string.Empty;

        public string ThumbnailPath { get; private set; } = string.Empty;

        public string UserId { get; private set; } = string.Empty;
        public bool Processed { get; private set; }
        public DateTime UploadedAt { get; private set; }
        public DateTime? ProcessedAt { get; private set; }

        public bool Accepted { get; private set; }

        public DateTime AcceptedAt { get; private set; } = DateTime.UtcNow;

        public Document() { }

        public void Apply(DocumentUploaded e)
        {
            Id = e.AggregateId;
            UploadedAt = e.OccurredOn;
            UserId = e.UserId;
        }

        public void MarkAsProcessed()
        {
            if (Processed) throw new InvalidOperationException("Document is already processed.");
            Processed = true;
            ProcessedAt = DateTime.UtcNow;
        }
    }
}
