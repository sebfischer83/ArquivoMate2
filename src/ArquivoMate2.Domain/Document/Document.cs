using ArquivoMate2.Domain.Import;
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
        public bool Accepted { get; private set; }

        public bool Processed { get; private set; }

        public bool Deleted { get; private set; }

        // content
        public string Content { get; private set; } = string.Empty;

        public string Hash { get; private set; } = string.Empty;

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

        private string? _initialTitle;

        public Document() { }

        public void Apply(DocumentUploaded e)
        {
            Id = e.AggregateId;
            UserId = e.UserId;
            OccurredOn = e.OccurredOn;
            Hash = e.Hash;
        }

        public void Apply(DocumentTitleInitialized e)
        {
            if (string.IsNullOrWhiteSpace(Title))
            {
                Title = e.Title;
                _initialTitle = e.Title;
            }
            OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentTitleSuggested e)
        {
            if (string.IsNullOrWhiteSpace(Title) || (!string.IsNullOrWhiteSpace(_initialTitle) && Title == _initialTitle))
            {
                Title = e.Title;
            }
            OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentContentExtracted e)
        {
            Content = e.Content;

            OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentFilesPrepared e)
        {
            FilePath = e.FilePath;
            MetadataPath = e.MetadataPath;
            ThumbnailPath = e.ThumbnailPath;
            PreviewPath = e.PreviewPath;
            OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentProcessed e)
        {
            Processed = true;
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
            if (!string.IsNullOrWhiteSpace(Title) && Title != _initialTitle)
                _initialTitle = null;
            OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentDeleted e)
        {
            Deleted = true;
            OccurredOn = e.OccurredOn;
        }
    }
}
