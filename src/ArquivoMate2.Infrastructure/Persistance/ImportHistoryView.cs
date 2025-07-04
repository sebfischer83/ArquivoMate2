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
        public Guid Id { get; set; } // Eindeutige ID des Imports
        public string FileName { get; set; } = string.Empty; // Name der hochgeladenen Datei
        public string UserId { get; set; } = string.Empty; // ID des Benutzers, der den Import initiiert hat
        public ImportSource Source { get; set; } = ImportSource.User; // Quelle des Imports (Benutzer oder Email)
        public DateTime StartedAt { get; set; } // Zeitpunkt des Starts des Imports
        public DateTime? CompletedAt { get; set; } // Zeitpunkt des Abschlusses (falls erfolgreich)
        public DocumentProcessingStatus Status { get; set; } = DocumentProcessingStatus.Pending; // Status des Imports
        public string? ErrorMessage { get; set; } // Fehlermeldung (falls fehlgeschlagen)
        public Guid? DocumentId { get; set; } // Verknüpfte Dokument-ID (falls erfolgreich)

        public bool IsCompleted => Status == DocumentProcessingStatus.Completed;

        public bool IsFailed => Status == DocumentProcessingStatus.Failed;

        public bool IsPending => Status == DocumentProcessingStatus.Pending;

        public bool IsInProgress => Status == DocumentProcessingStatus.InProgress;

        public bool IsHidden { get; set; } = false;

        public DateTime OccurredOn { get; set; }

        // Helper properties for UI display
        public bool IsFromUser => Source == ImportSource.User;
        public bool IsFromEmail => Source == ImportSource.Email;
    }
}
