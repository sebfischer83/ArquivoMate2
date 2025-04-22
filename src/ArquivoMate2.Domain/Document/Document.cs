namespace ArquivoMate2.Domain.Document
{

    public class Document
    {
        public Guid Id { get; private set; }
        public string FilePath { get; private set; }
        public bool Processed { get; private set; }
        public DateTime UploadedAt { get; private set; }
        public DateTime? ProcessedAt { get; private set; }

        public Document() { }

        public static Document Create(DocumentUploaded evt)
        {
            var doc = new Document();
            doc.Apply(evt);
            return doc;
        }

        public void Apply(DocumentUploaded e)
        {
            Id = e.AggregateId;
            FilePath = e.FilePath;
            UploadedAt = e.OccurredOn;
        }

        public void MarkAsUploaded(DocumentUploaded @event)
        {
            Id = @event.AggregateId;
            FilePath = @event.FilePath;
        }

        public void MarkAsProcessed()
        {
            if (Processed) throw new InvalidOperationException("Document is already processed.");
            Processed = true;
        }
    }
}
