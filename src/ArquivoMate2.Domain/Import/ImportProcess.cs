using ArquivoMate2.Domain.Document;
using ArquivoMate2.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Domain.Import
{
    /// <summary>
    /// Represents the lifecycle of a document import from initiation to completion.
    /// </summary>
    public class ImportProcess
    {
        public Guid Id { get; private set; } // Unique identifier of the import
        public string FileName { get; private set; } = string.Empty; // Original name of the uploaded file
        public string UserId { get; private set; } = string.Empty; // Identifier of the user who triggered the import
        public ImportSource Source { get; private set; } = ImportSource.User; // Source of the import (user upload or email)
        public DateTime StartedAt { get; private set; } // Timestamp when the import started processing
        public DateTime? CompletedAt { get; private set; } // Timestamp when the import finished successfully
        public DocumentProcessingStatus Status { get; private set; } = DocumentProcessingStatus.Pending; // Current processing status
        public string? ErrorMessage { get; private set; } // Error message captured for failed imports
        public Guid? DocumentId { get; private set; } // Identifier of the resulting document when processing succeeded

        public bool IsHidden { get; private set; } = false; // Indicates whether the import should be hidden in the UI

        public DateTime OccurredOn { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportProcess"/> class.
        /// </summary>
        public ImportProcess()
        {

        }

        /// <summary>
        /// Applies the initial import event to set up process metadata.
        /// </summary>
        /// <param name="e">Initial import event.</param>
        public void Apply(InitDocumentImport e)
        {
            Id = e.AggregateId;
            UserId = e.UserId;
            FileName = e.FileName;
            Source = e.Source;
            OccurredOn = e.OccurredOn;
            StartedAt = e.OccurredOn;
            Status = DocumentProcessingStatus.Pending;
        }

        /// <summary>
        /// Marks the import as in progress.
        /// </summary>
        /// <param name="e">Start import event.</param>
        public void Apply(StartDocumentImport e)
        {
            Id = e.AggregateId;
            OccurredOn = e.OccurredOn;
            Status = DocumentProcessingStatus.InProgress;
        }

        /// <summary>
        /// Marks the import as failed and captures the error message.
        /// </summary>
        /// <param name="e">Failure event.</param>
        public void Apply(MarkFailedDocumentImport e)
        {
            Id = e.AggregateId;
            OccurredOn = e.OccurredOn;
            Status = DocumentProcessingStatus.Failed;
            ErrorMessage = e.ErrorMessage;
            CompletedAt = e.OccurredOn;
        }

        /// <summary>
        /// Marks the import as completed successfully and associates the resulting document.
        /// </summary>
        /// <param name="e">Success event.</param>
        public void Apply(MarkSucceededDocumentImport e)
        {
            Id = e.AggregateId;
            OccurredOn = e.OccurredOn;
            Status = DocumentProcessingStatus.Completed;
            DocumentId = e.DocumentId;
            CompletedAt = e.OccurredOn;
        }

        /// <summary>
        /// Hides the import from user-facing lists.
        /// </summary>
        /// <param name="e">Hide event.</param>
        public void Apply(HideDocumentImport e)
        {
            Id = e.AggregateId;
            OccurredOn = e.OccurredOn;
            IsHidden = true;
        }
    }
}
