using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Shared.Models
{
    public class DocumentDto
    {
        public Guid Id { get; set; }

        public string FilePath { get; set; } = string.Empty;

        public string ThumbnailPath { get; set; } = string.Empty;

        public string MetadataPath { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;

        public bool Processed { get; set; }

        public DateTime UploadedAt { get; set; }

        public DateTime? ProcessedAt { get; set; }

        public string Content { get; set; } = string.Empty;

        public bool Accepted { get; set; }

        public DateTime AcceptedAt { get; set; }
    }
}
