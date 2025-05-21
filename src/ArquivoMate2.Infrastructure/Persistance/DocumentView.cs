using ArquivoMate2.Shared.Models;

namespace ArquivoMate2.Infrastructure.Persistance
{
    public class DocumentView
    {
        public Guid Id { get; set; }
        public string FilePath { get; set; } = string.Empty;

        public string ThumbnailPath { get; set; } = string.Empty;

        public string MetadataPath { get; set; } = string.Empty;
        public string PreviewPath { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;
        public ProcessingStatus Status { get; set; }
        public DateTime UploadedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }

        public string Content { get; set; } = string.Empty;

        public bool Accepted { get; set; }

        public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;

        public string Type { get; set; } = string.Empty;

        public string CustomerNumber { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal? TotalPrice { get; set; }
        public List<string> Keywords { get; set; } = new List<string>();
        public string Summary { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
    }
}
