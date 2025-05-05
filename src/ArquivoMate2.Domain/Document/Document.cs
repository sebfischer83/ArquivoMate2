namespace ArquivoMate2.Domain.Document
{

    public class Document
    {
        public Guid Id { get; private set; }
        public string FilePath { get; private set; } = string.Empty;

        public string ThumbnailPath { get; private set; } = string.Empty;

        public string MetadataPath { get; private set; } = string.Empty;

        public string UserId { get; private set; } = string.Empty;
        public bool Processed { get; private set; }
        public DateTime UploadedAt { get; private set; }
        public DateTime? ProcessedAt { get; private set; }

        public bool Accepted { get; private set; }

        public DateTime? AcceptedAt { get; private set; }

        // content
        public string Content { get; private set; } = string.Empty;

        public DateTime? Date { get; private set; } = null;

        public Guid Sender { get; private set; } = Guid.Empty;

        public Guid Recipient { get; private set; } = Guid.Empty;

        public string Type { get; set; } = string.Empty;

        public string CustomerNumber { get; private set; } = string.Empty;
        public string InvoiceNumber { get; private set; } = string.Empty;
        public decimal? TotalPrice { get; private set; }
        public List<string> Keywords { get; private set; } = new List<string>();
        public string Summary { get; private set; } = string.Empty;

        public Document() { }

        public void Apply(DocumentUploaded e)
        {
            Id = e.AggregateId;
            UploadedAt = e.OccurredOn;
            UserId = e.UserId;
        }

        public void Apply(DocumentContentExtracted documentContentExtracted)
        {
            Content = documentContentExtracted.Content;
        }

        public void Apply(DocumentFilesPrepared e)
        {
            FilePath = e.FilePath;
            MetadataPath = e.MetadataPath;
            ThumbnailPath = e.ThumbnailPath;
        }

        public void MarkAsProcessed()
        {
            if (Processed) throw new InvalidOperationException("Document is already processed.");
            Processed = true;
            ProcessedAt = DateTime.UtcNow;
        }

        public void Apply(DocumentChatBotDataReceived e)
        {
            Sender = e.SenderId;
            Recipient = e.RecipientId;
            Date = e.Date;
            Type = e.Type;
            CustomerNumber = e.CustomerNumber;
            InvoiceNumber = e.InvoiceNumber;
            TotalPrice = e.TotalPrice;
            Keywords = e.Keywords;
            Summary = e.Summary;
        }
    }
}
