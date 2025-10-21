using System;

namespace ArquivoMate2.Domain.DocumentTypes
{
    /// <summary>
    /// Associates a user with a document type definition that is available to them.
    /// </summary>
    public class UserDocumentType
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UserId { get; set; } = string.Empty;
        public Guid DocumentTypeId { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
