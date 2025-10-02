using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Shared.Models
{
    public class DocumentListItemDto : BaseDto
    {
        public string ThumbnailPath { get; set; } = string.Empty;

        public List<string> Keywords { get; set; } = new List<string>();
        public string Summary { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public bool Accepted { get; set; }

        public string Title { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; }

        public bool Encrypted { get; set; } // NEW
    }
}
