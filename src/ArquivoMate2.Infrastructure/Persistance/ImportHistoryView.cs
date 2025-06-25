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
        public DateTime StartedAt { get; set; } // Zeitpunkt des Starts des Imports
        public DateTime? CompletedAt { get; set; } // Zeitpunkt des Abschlusses (falls erfolgreich)
        public DocumentProcessingStatus Status { get; set; } = DocumentProcessingStatus.Pending; // Status des Imports
        public string? ErrorMessage { get; set; } // Fehlermeldung (falls fehlgeschlagen)
        public Guid? DocumentId { get; set; } // Verknüpfte Dokument-ID (falls erfolgreich)

        public DateTime OccurredOn { get; set; }
    }
}
