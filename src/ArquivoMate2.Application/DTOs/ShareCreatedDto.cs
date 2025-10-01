namespace ArquivoMate2.Application.DTOs
{
    public class ShareCreatedDto
    {
        public Guid ShareId { get; set; }
        public string Artifact { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public string Url { get; set; } = string.Empty;
    }
}
