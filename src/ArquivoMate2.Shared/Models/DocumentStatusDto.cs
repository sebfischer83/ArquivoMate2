using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Shared.Models
{
    public class DocumentStatusDto
    {
        public Guid DocumentId { get; set; }
        public bool IsProcessed { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}
