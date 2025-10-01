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
        public string ArchivePath { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime? OccurredOn { get; set; } = null;

        /// <summary>
        /// The document processing is finished
        /// </summary>
        public bool Processed { get; set; }

        public bool Deleted { get; set; }

        public string Content { get; set; } = string.Empty;

        public bool Accepted { get; set; }

        public DateTime AcceptedAt { get; set; }

        public string Type { get; set; } = string.Empty;

        public string CustomerNumber { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal? TotalPrice { get; set; }
        public List<string> Keywords { get; set; } = new List<string>();
        public string Summary { get; set; } = string.Empty;
        public DateTime? Date { get; set; }

        public string Title { get; set; } = string.Empty;
        public int ContentLength { get; set; }

        public DateTime UploadedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }

        public string ChatBotModel { get; set; } = string.Empty; // LLM model name used for enrichment
        public string ChatBotClass { get; set; } = string.Empty; // LLM-provided classification label

        public int NotesCount { get; set; } // Number of notes currently associated with the document

        public string Language { get; set; } = string.Empty; // ISO code of the detected document language

        public bool Encrypted { get; set; } // Indicates whether encrypted delivery must be used
    }
}
