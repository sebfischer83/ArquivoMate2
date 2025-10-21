using System.ComponentModel.DataAnnotations;

namespace ArquivoMate2.Shared.Models.DocumentTypes
{
    public class UpdateDocumentTypeRequest
    {
        [Required(AllowEmptyStrings = false)]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;
        [StringLength(100)]
        public string SystemFeature { get; set; } = string.Empty;
        [StringLength(500)]
        public string UserDefinedFunction { get; set; } = string.Empty;
    }
}
