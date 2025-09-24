using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.Import;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using Marten.Events.Projections;
using Marten.Linq.SoftDeletes;
using Marten.Schema;

namespace ArquivoMate2.Infrastructure.Persistance
{

    public class DocumentProjection : SingleStreamProjection<DocumentView, Guid>
    {
        public void Apply(DocumentUploaded e, DocumentView view)
        {
            view.Id = e.AggregateId;
            view.UserId = e.UserId;
            view.OccurredOn = e.OccurredOn;
            view.UploadedAt = e.OccurredOn; // neu
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
                    {
                        prop.SetValue(view, enumerable.ToList());
                    }
                    else if (prop.PropertyType.IsEnum && kvp.Value is string enumString)
                    {
                        var enumValue = Enum.Parse(prop.PropertyType, enumString);
                        prop.SetValue(view, enumValue);
                    }
                    else if (kvp.Value == null || prop.PropertyType.IsInstanceOfType(kvp.Value))
                    {
                        prop.SetValue(view, kvp.Value);
                    }
                    else
                    {
                        var converted = Convert.ChangeType(kvp.Value, prop.PropertyType);
                        prop.SetValue(view, converted);
                    }
                }
            }
            view.OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentProcessed e, DocumentView view)
        {
            view.Processed = true;
            view.OccurredOn = e.OccurredOn;
            view.ProcessedAt = e.OccurredOn; // neu
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
