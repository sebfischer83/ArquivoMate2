using System.Collections.Generic;

namespace ArquivoMate2.Shared.Models
{
    public class DocumentAnswerDto
    {
        public string Answer { get; set; } = string.Empty;

        public string Model { get; set; } = string.Empty;

        public List<DocumentAnswerCitationDto> Citations { get; set; } = new();
    }

    public class DocumentAnswerCitationDto
    {
        public string? Source { get; set; }

        public string Snippet { get; set; } = string.Empty;
    }
}
