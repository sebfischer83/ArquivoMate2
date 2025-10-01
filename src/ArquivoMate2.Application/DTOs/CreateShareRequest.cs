namespace ArquivoMate2.Application.DTOs
{
    public class CreateShareRequest
    {
        public Guid DocumentId { get; set; }
        public string? Artifact { get; set; }
        public int? TtlMinutes { get; set; }
    }
}
