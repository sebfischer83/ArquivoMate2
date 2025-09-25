using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.Import;
using Marten.Events.Aggregation;

namespace ArquivoMate2.Infrastructure.Persistance
{
    public class DocumentProjection : SingleStreamProjection<DocumentView, Guid>
    {
        public void Apply(DocumentUploaded e, DocumentView view)
        {
            view.Id = e.AggregateId;
            view.UserId = e.UserId;
            view.OccurredOn = e.OccurredOn;
            view.UploadedAt = e.OccurredOn; // neu gesetzt
        }

        public void Apply(DocumentTitleInitialized e, DocumentView view)
        {
            if (string.IsNullOrWhiteSpace(view.Title))
                view.Title = e.Title;
            view.OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentTitleSuggested e, DocumentView view)
        {
            view.Title = e.Title; // immer überschreiben (Logik bereits im Aggregate abgesichert)
            view.OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentContentExtracted e, DocumentView view)
        {
            view.Content = e.Content;
            view.ContentLength = e.Content?.Length ?? 0;
            view.OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentUpdated e, DocumentView view)
        {
            var type = view.GetType();
            foreach (var kvp in e.Values)
            {
                var prop = type.GetProperty(kvp.Key);
                if (prop != null && prop.CanWrite)
                {
                    if (prop.PropertyType == typeof(List<string>) && kvp.Value is IEnumerable<string> enumerable)
                        prop.SetValue(view, enumerable.ToList());
                    else if (prop.PropertyType.IsEnum && kvp.Value is string enumString)
                        prop.SetValue(view, Enum.Parse(prop.PropertyType, enumString));
                    else if (kvp.Value == null || prop.PropertyType.IsInstanceOfType(kvp.Value))
                        prop.SetValue(view, kvp.Value);
                    else
                        prop.SetValue(view, Convert.ChangeType(kvp.Value, prop.PropertyType));
                }
            }
            view.OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentProcessed e, DocumentView view)
        {
            view.Processed = true;
            view.OccurredOn = e.OccurredOn;
            view.ProcessedAt = e.OccurredOn; // neu gesetzt
        }

        public void Apply(DocumentChatBotDataReceived e, DocumentView view)
        {
            view.Keywords = e.Keywords;
            view.Summary = e.Summary;
            view.CustomerNumber = e.CustomerNumber;
            view.InvoiceNumber = e.InvoiceNumber;
            view.TotalPrice = e.TotalPrice;
            view.Type = e.Type;
            view.Date = e.Date;
            view.OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentFilesPrepared e, DocumentView view)
        {
            view.FilePath = e.FilePath;
            view.MetadataPath = e.MetadataPath;
            view.ThumbnailPath = e.ThumbnailPath;
            view.PreviewPath = e.PreviewPath;
            view.OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentDeleted e, DocumentView view)
        {
            view.Deleted = true;
            view.OccurredOn = e.OccurredOn;
        }
    }
}
