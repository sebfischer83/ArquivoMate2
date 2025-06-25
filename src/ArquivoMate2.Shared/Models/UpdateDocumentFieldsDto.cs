namespace ArquivoMate2.Shared.Models
{
    public class UpdateDocumentFieldsDto
    {
        public required Dictionary<string, object> Fields { get; set; }
    }
}