namespace ArquivoMate2.Application.Features.Processors.LabResults.Domain
{
    public sealed class LabResultPoint
    {
        public string ResultRaw { get; set; } = default!;
        public decimal? ResultNumeric { get; set; }                 // parst z. B. „13,2“ → 13.2
        public string? ResultComparator { get; set; }               // e.g. "<", ">=", stored when present
        public string? Unit { get; set; }                           // Einheit kann je Datum variieren
        public string? Reference { get; set; }
        public string? ReferenceComparator { get; set; }            // e.g. "<", ">=", when present for reference

        public decimal? ReferenceFrom { get; set; }                  // parst z. B. „10" → 10.0

        public decimal? ReferenceTo { get; set; }                    // parst z. B. „20" → 20.0
    }
}
