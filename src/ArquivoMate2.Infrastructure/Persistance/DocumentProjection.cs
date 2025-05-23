﻿using ArquivoMate2.Domain.Document;
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
        }

        public void Apply(DocumentProcessed e, DocumentView view)
        {
            view.Status = Shared.Models.ProcessingStatus.Completed;
            view.ProcessedAt = e.OccurredOn;
        }

        public void Apply(DocumentContentExtracted e, DocumentView view)
        {
            view.Content = e.Content;
        }

        public void Apply(DocumentStartProcessing e, DocumentView view)
        {
            view.Status = Shared.Models.ProcessingStatus.InProgress;
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
        }

        public void Apply(DocumentFilesPrepared e, DocumentView view)
        {
            view.FilePath = e.FilePath;
            view.MetadataPath = e.MetadataPath;
            view.ThumbnailPath = e.ThumbnailPath;
            view.PreviewPath = e.PreviewPath;
        }
    }
}
