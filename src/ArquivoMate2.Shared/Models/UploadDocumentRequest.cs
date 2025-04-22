using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Shared.Models
{
    public class UploadDocumentRequest
    {
        public IFormFile File { get; set; } = default!;
    }
}
