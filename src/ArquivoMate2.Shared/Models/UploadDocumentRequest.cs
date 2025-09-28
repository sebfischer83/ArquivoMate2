using Microsoft.AspNetCore.Http;

namespace ArquivoMate2.Shared.Models
{
    public class UploadDocumentRequest
    {
        public IFormFile File { get; set; } = default!; // Only file; language auto-detected later
    }
}
