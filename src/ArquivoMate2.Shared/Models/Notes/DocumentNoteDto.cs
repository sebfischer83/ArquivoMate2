namespace ArquivoMate2.Shared.Models.Notes
{
    public class DocumentNoteDto
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
