namespace ArquivoMate2.Domain.Notes
{
    public class DocumentNote
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
