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

        public string UserId { get; set; } = string.Empty;

        public bool Processed { get; set; }
        
        public DateTime UploadedAt { get; set; }

        public DateTime? ProcessedAt { get; set; }

        public string Content { get; set; } = string.Empty;

        public int ContentLength { get; set; }

        public bool Accepted { get; set; }

        public DateTime AcceptedAt { get; set; }

        public ProcessingStatus Status { get; set; } = ProcessingStatus.Pending;

        public string Type { get; set; } = string.Empty;

        public string CustomerNumber { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal? TotalPrice { get; set; }
        public List<string> Keywords { get; set; } = new List<string>();
        public string Summary { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public List<DocumentEventDto> History { get; set; } = new List<DocumentEventDto>();
    }
}
