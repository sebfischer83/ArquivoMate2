using ArquivoMate2.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Persistance
{
    public class ImportHistoryView
    {
        public Guid Id { get; set; } // Unique identifier of the import
        public string FileName { get; set; } = string.Empty; // Original name of the uploaded file
        public string UserId { get; set; } = string.Empty; // Identifier of the user who initiated the import
        public ImportSource Source { get; set; } = ImportSource.User; // Source of the import (user upload or email)
        public DateTime StartedAt { get; set; } // Timestamp when the import started processing
        public DateTime? CompletedAt { get; set; } // Timestamp when the import finished successfully
        public DocumentProcessingStatus Status { get; set; } = DocumentProcessingStatus.Pending; // Current processing status
        public string? ErrorMessage { get; set; } // Error message captured for failed imports
        public Guid? DocumentId { get; set; } // Identifier of the resulting document when processing succeeded

        public bool IsCompleted => Status == DocumentProcessingStatus.Completed;

        public bool IsFailed => Status == DocumentProcessingStatus.Failed;

        public bool IsPending => Status == DocumentProcessingStatus.Pending;

        public bool IsInProgress => Status == DocumentProcessingStatus.InProgress;

        public bool IsHidden { get; set; } = false;

        public DateTime OccurredOn { get; set; }

        // Helper properties for UI display
        public bool IsFromUser => Source == ImportSource.User;
        public bool IsFromEmail => Source == ImportSource.Email;

        public bool IsFromIngestion => Source == ImportSource.Ingestion;
    }
}
