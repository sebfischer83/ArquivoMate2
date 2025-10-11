using System;
using System.Collections.Generic;

namespace ArquivoMate2.Shared.Models
{
    public class DocumentAnswerDto
    {
        public string Answer { get; set; } = string.Empty;

        public string Model { get; set; } = string.Empty;

        public List<DocumentAnswerCitationDto> Citations { get; set; } = new();

        public List<DocumentAnswerReferenceDto> Documents { get; set; } = new();

        public long? DocumentCount { get; set; }
    }

    public class DocumentAnswerCitationDto
    {
        public string? Source { get; set; }

        public string Snippet { get; set; } = string.Empty;
    }

    public class DocumentAnswerReferenceDto
    {
        public Guid DocumentId { get; set; }

        public string? Title { get; set; }

        public string? Summary { get; set; }

        public DateTime? Date { get; set; }

        public double? Score { get; set; }

        public long? FileSizeBytes { get; set; }
    }
}
