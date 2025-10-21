using System;

namespace ArquivoMate2.Application.Models
{
    /// <summary>
    /// Simplified representation of a document type that can be used by application services and chat bots.
    /// </summary>
    public class DocumentTypeOption
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
    }
}
