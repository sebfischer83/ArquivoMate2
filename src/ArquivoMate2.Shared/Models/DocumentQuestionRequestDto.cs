using System.ComponentModel.DataAnnotations;

namespace ArquivoMate2.Shared.Models
{
    public class DocumentQuestionRequestDto
    {
        [Required]
        [MaxLength(2000)]
        public string Question { get; set; } = string.Empty;

        public bool IncludeHistory { get; set; }
    }
}
