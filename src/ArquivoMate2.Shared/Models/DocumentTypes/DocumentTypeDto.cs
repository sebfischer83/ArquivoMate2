using System;

namespace ArquivoMate2.Shared.Models.DocumentTypes
{
    public class DocumentTypeDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SystemFeature { get; set; } = string.Empty;
        public string UserDefinedFunction { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
        public bool IsAssignedToCurrentUser { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
