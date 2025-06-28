using System;

namespace ArquivoMate2.Shared.Models
{
    public class ImportHistoryListItemDto
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public Guid? DocumentId { get; set; }
        public DateTime OccurredOn { get; set; }
    }
}
