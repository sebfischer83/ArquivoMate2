namespace ArquivoMate2.Infrastructure.Persistance
{
    public class DocumentView
    {
        public Guid Id { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public bool Processed { get; set; }
        public DateTime UploadedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }
}
