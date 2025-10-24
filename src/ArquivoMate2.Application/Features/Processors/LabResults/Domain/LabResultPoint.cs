namespace ArquivoMate2.Application.Features.Processors.LabResults.Domain
{
    public sealed class LabResultPoint
    {
        // Parameter name as extracted from the report (e.g. "Hemoglobin")
        public string Parameter { get; set; } = string.Empty;

        public string ResultRaw { get; set; } = default!;
        public decimal? ResultNumeric { get; set; }                 // parst z. B. „13,2“ → 13.2
        public string? ResultComparator { get; set; }               // e.g. "<", ">=", stored when present
        public string? Unit { get; set; }                           // Einheit kann je Datum variieren
        public string? Reference { get; set; }
        public string? ReferenceComparator { get; set; }            // e.g. "<", ">=", when present for reference

        public decimal? ReferenceFrom { get; set; }                  // parst z. B. „10" → 10.0

        public decimal? ReferenceTo { get; set; }                    // parst z. B. „20" → 20.0

        // Normalized values: canonical representations computed from the raw fields to simplify downstream
        // processing and comparisons. These are optional and may be null if normalization failed.
        public decimal? NormalizedResult { get; set; }
        public string? NormalizedUnit { get; set; }
        public decimal? NormalizedReferenceFrom { get; set; }
        public decimal? NormalizedReferenceTo { get; set; }
    }
}
