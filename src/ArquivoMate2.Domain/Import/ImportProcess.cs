using ArquivoMate2.Domain.Document;
using ArquivoMate2.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Domain.Import
{
    public class ImportProcess
    {
        public Guid Id { get; private set; } // Eindeutige ID des Imports
        public string FileName { get; private set; } = string.Empty; // Name der hochgeladenen Datei
        public string UserId { get; private set; } = string.Empty; // ID des Benutzers, der den Import initiiert hat
        public ImportSource Source { get; private set; } = ImportSource.User; // Quelle des Imports (Benutzer oder Email)
        public DateTime StartedAt { get; private set; } // Zeitpunkt des Starts des Imports
        public DateTime? CompletedAt { get; private set; } // Zeitpunkt des Abschlusses (falls erfolgreich)
        public DocumentProcessingStatus Status { get; private set; } = DocumentProcessingStatus.Pending; // Status des Imports
        public string? ErrorMessage { get; private set; } // Fehlermeldung (falls fehlgeschlagen)
        public Guid? DocumentId { get; private set; } // Verknüpfte Dokument-ID (falls erfolgreich)

        public bool IsHidden { get; private set; } = false; // Gibt an, ob der Import in der UI versteckt ist

        public DateTime OccurredOn { get; private set; }

        public ImportProcess()
        {
            
        }

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

        public void Apply(StartDocumentImport e)
        {
            Id = e.AggregateId;
            OccurredOn = e.OccurredOn;
            Status = DocumentProcessingStatus.InProgress;
        }

        public void Apply(MarkFailedDocumentImport e)
        {
            Id = e.AggregateId;
            OccurredOn = e.OccurredOn;
            Status = DocumentProcessingStatus.Failed;
            ErrorMessage = e.ErrorMessage;
        }

        public void Apply(MarkSuccededDocumentImport e)
        {
            Id = e.AggregateId;
            OccurredOn = e.OccurredOn;
            Status = DocumentProcessingStatus.Completed;
            DocumentId = e.DocumentId;
        }

        public void Apply(HideDocumentImport e)
        {
            Id = e.AggregateId;
            OccurredOn = e.OccurredOn;
            IsHidden = true;
        }
    }
}
