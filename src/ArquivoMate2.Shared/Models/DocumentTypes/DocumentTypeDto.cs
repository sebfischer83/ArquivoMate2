using System;
using System.Collections.Generic;

namespace ArquivoMate2.Shared.Models.DocumentTypes
{
    public class DocumentTypeDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // Multiple system feature keys associated with this type
        public List<string> SystemFeatures { get; set; } = new();

        // Multiple user-defined functions associated with this type
        public List<string> UserDefinedFunctions { get; set; } = new();

        public bool IsLocked { get; set; }
        public bool IsAssignedToCurrentUser { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
