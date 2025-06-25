using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.Import;
using Marten.Events.Aggregation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Persistance
{
    public class ImportHistoryProjection : SingleStreamProjection<ImportHistoryView>
    {
        public void Apply(InitDocumentImport e, ImportHistoryView view)
        {
            view.Id = e.AggregateId;
            view.UserId = e.UserId;
            view.OccurredOn = e.OccurredOn;
            view.FileName = e.FileName;
            view.Status = Shared.Models.DocumentProcessingStatus.Pending;
        }

        public void Apply(StartDocumentImport e, ImportHistoryView view)
        {
            view.Id = e.AggregateId;
            view.Status = Shared.Models.DocumentProcessingStatus.InProgress;
            view.OccurredOn = e.OccurredOn;
        }

        public void Apply(MarkFailedDocumentImport e, ImportHistoryView view)
        {
            view.Id = e.AggregateId;
            view.Status = Shared.Models.DocumentProcessingStatus.Failed;
            view.OccurredOn = e.OccurredOn;
            view.ErrorMessage = e.ErrorMessage;
        }

        public void Apply(MarkSuccededDocumentImport e, ImportHistoryView view)
        {
            if (view.Id == view.DocumentId)
            {
                return;
            }

            view.Id = e.AggregateId;
            view.DocumentId = e.DocumentId;
            view.Status = Shared.Models.DocumentProcessingStatus.Completed;
            view.OccurredOn = e.OccurredOn;
        }
    }
}
