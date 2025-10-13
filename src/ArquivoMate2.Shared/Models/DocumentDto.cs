using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Shared.Models
{
    public class BaseDto
    {
        public Guid Id { get; set; }
    }

    public class DocumentDto : BaseDto
    {
        public string FilePath { get; set; } = string.Empty;

        public string ThumbnailPath { get; set; } = string.Empty;

        public string MetadataPath { get; set; } = string.Empty;

        public string PreviewPath { get; set; } = string.Empty;
        public string ArchivePath { get; set; } = string.Empty; // NEW

        public string UserId { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; }

        public DateTime? ProcessedAt { get; set; }

        public string Content { get; set; } = string.Empty;

        public int ContentLength { get; set; }

        public bool Accepted { get; set; }

        public DateTime AcceptedAt { get; set; }

        public string Type { get; set; } = string.Empty;

        public string CustomerNumber { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal? TotalPrice { get; set; }
        public List<string> Keywords { get; set; } = new List<string>();
        public string Summary { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string ChatBotModel { get; set; } = string.Empty; // NOVO
        public string ChatBotClass { get; set; } = string.Empty; // NOVO

        public int NotesCount { get; set; } // NEW

        public string Language { get; set; } = string.Empty; // NEW
        public bool Encrypted { get; set; } // NEW
        public DocumentEncryptionType EncryptionType { get; set; } // NEW

        public List<DocumentEventDto> History { get; set; } = new List<DocumentEventDto>();

        // Resolved sender details
        public PartyDto? Sender { get; set; }
    }
}
