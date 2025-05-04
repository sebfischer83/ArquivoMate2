namespace ArquivoMate2.Application.Models
{
    public class DocumentAnalysisResult
    {
        public DateTime Date { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public PartyInfo Sender { get; set; } = new PartyInfo();
        public PartyInfo Recipient { get; set; } = new PartyInfo();
        public string CustomerNumber { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public List<string> Keywords { get; set; } = new();
        public string Summary { get; set; } = string.Empty;

        public decimal? TotalPrice { get; set; }
    }
}
