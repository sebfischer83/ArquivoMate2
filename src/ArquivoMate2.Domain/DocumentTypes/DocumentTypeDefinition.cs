using System;
using System.Collections.Generic;

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

        // Upper-cased normalized name used for case-insensitive unique constraint.
        public string NormalizedName { get; set; } = string.Empty;

        // Optional system feature identifiers for this seeded type (e.g. 'invoicing').
        // Multiple features are supported.
        public List<string> SystemFeatures { get; set; } = new();

        // Optional user-defined functions associated with user-created types.
        // Stored as-is; execution/validation happens elsewhere. Multiple allowed.
        public List<string> UserDefinedFunctions { get; set; } = new();

        public bool IsLocked { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
