using ArquivoMate2.Shared.Models;

namespace ArquivoMate2.Domain.Document
{

    public class Document
    {
        public Guid Id { get; private set; }
        public string FilePath { get; private set; } = string.Empty;

        public string ThumbnailPath { get; private set; } = string.Empty;

        public string MetadataPath { get; private set; } = string.Empty;

        public string PreviewPath { get; private set; } = string.Empty;

        public string UserId { get; private set; } = string.Empty;
        public DateTime UploadedAt { get; private set; }
        public bool Accepted { get; private set; }

        public ProcessingStatus Status { get; private set; } = ProcessingStatus.Pending;

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

        public string Title { get; private set; } = string.Empty;

        public DateTime? OccurredOn { get; private set; }

        public Document() { }

        public void Apply(DocumentUploaded e)
        {
            Id = e.AggregateId;
            UploadedAt = e.OccurredOn;
            UserId = e.UserId;
            Status = ProcessingStatus.Pending;
            OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentContentExtracted documentContentExtracted)
        {
            Content = documentContentExtracted.Content;

            OccurredOn = documentContentExtracted.OccurredOn;
        }

        public void Apply(DocumentFilesPrepared e)
        {
            FilePath = e.FilePath;
            MetadataPath = e.MetadataPath;
            ThumbnailPath = e.ThumbnailPath;
            PreviewPath = e.PreviewPath;
            OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentStartProcessing e)
        {
            Status = ProcessingStatus.InProgress;
            OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentProcessed e)
        {
            if (Status == ProcessingStatus.Completed) throw new InvalidOperationException("Document is already processed.");
            Status = ProcessingStatus.Completed;
            OccurredOn = e.OccurredOn;
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
            OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentUpdated e)
        {
            var type = this.GetType();
            foreach (var kvp in e.Values)
            {
                var prop = type.GetProperty(kvp.Key);
                if (prop != null && prop.CanWrite)
                {
                    // Handle List<string> conversion if needed
                    if (prop.PropertyType == typeof(List<string>) && kvp.Value is IEnumerable<string> enumerable)
                    {
                        prop.SetValue(this, enumerable.ToList());
                    }
                    else if (prop.PropertyType.IsEnum && kvp.Value is string enumString)
                    {
                        var enumValue = Enum.Parse(prop.PropertyType, enumString);
                        prop.SetValue(this, enumValue);
                    }
                    else if (kvp.Value == null || prop.PropertyType.IsInstanceOfType(kvp.Value))
                    {
                        prop.SetValue(this, kvp.Value);
                    }
                    else
                    {
                        // Try to convert value to property type
                        var converted = Convert.ChangeType(kvp.Value, prop.PropertyType);
                        prop.SetValue(this, converted);
                    }
                }
            }
            OccurredOn = e.OccurredOn;
        }
    }
}
