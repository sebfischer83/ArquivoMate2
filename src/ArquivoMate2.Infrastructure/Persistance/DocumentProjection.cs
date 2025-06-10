using ArquivoMate2.Domain.Document;
using Marten.Events.Aggregation;
using Marten.Events.Projections;
using Marten.Schema;

namespace ArquivoMate2.Infrastructure.Persistance
{

    public class DocumentProjection : SingleStreamProjection<DocumentView>
    {
        public void Apply(DocumentUploaded e, DocumentView view)
        {
            view.Status = Shared.Models.ProcessingStatus.Pending;
            view.Id = e.AggregateId;
            view.UploadedAt = e.OccurredOn;
            view.UserId = e.UserId;
            view.OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentProcessed e, DocumentView view)
        {
            view.Status = Shared.Models.ProcessingStatus.Completed;
            view.ProcessedAt = e.OccurredOn;
            view.OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentContentExtracted e, DocumentView view)
        {
            view.Content = e.Content;
            view.OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentStartProcessing e, DocumentView view)
        {
            view.Status = Shared.Models.ProcessingStatus.InProgress;
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
    }
}
