using System;

namespace ArquivoMate2.Domain.DocumentTypes
{
    /// <summary>
    /// Represents a document type that can be assigned to documents.
    /// Seeded types are locked and available to all users.
    /// </summary>
    public class DocumentTypeDefinition
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
