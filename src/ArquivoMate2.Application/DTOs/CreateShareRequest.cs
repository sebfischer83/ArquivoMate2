using ArquivoMate2.Shared.Models;

namespace ArquivoMate2.Application.DTOs
{
    public class CreateShareRequest
    {
        public Guid DocumentId { get; set; }
        public DocumentArtifact? Artifact { get; set; } // null -> default File
        public int? TtlMinutes { get; set; }
    }
}
