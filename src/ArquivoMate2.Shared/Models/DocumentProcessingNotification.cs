using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Shared.Models
{
    public enum DocumentProcessingStatus
    {
        InProgress,
        Completed,
        Failed
    }

    public class DocumentProcessingNotification
    {
        public string DocumentId { get; set; }
        public DocumentProcessingStatus Status { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public DocumentProcessingNotification(string documentId, DocumentProcessingStatus status, string message)
        {
            DocumentId = documentId;
            Status = status;
            Message = message;
            Timestamp = DateTime.UtcNow;
        }

        public static DocumentProcessingNotification InProgress(string documentId, string message)
        {
            return new DocumentProcessingNotification(documentId, DocumentProcessingStatus.InProgress, message);
        }

        public static DocumentProcessingNotification Completed(string documentId, string message)
        {
            return new DocumentProcessingNotification(documentId, DocumentProcessingStatus.Completed, message);
        }

        public static DocumentProcessingNotification Failed(string documentId, string message)
        {
            return new DocumentProcessingNotification(documentId, DocumentProcessingStatus.Failed, message);
        }
    }
}
