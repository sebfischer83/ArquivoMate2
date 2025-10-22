using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace ArquivoMate2.Shared.Models.DocumentTypes
{
    public class CreateDocumentTypeRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        public string SystemFeature { get; set; } = string.Empty; // legacy single value

        // New: optional array for multiple system features
        public List<string>? SystemFeatures { get; set; }

        // Optional user-defined function (script/expression) associated with this user-created type (legacy)
        [StringLength(500)]
        public string UserDefinedFunction { get; set; } = string.Empty;

        // New: allow multiple user-defined functions
        public List<string>? UserDefinedFunctions { get; set; }
    }
}
