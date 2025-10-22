namespace ArquivoMate2.Application.Features.Processors.LabResults.Domain.Parsing
{
    public sealed class ValueColumn
    {
        /// <summary>
        /// ISO 8601 date for this column (z. B. "2015-10-30").
        /// Tipp: In .NET 8/9 kannst du DateOnly verwenden. Wenn du es möchtest,
        /// dann ersetze string durch DateOnly und setze unten einen passenden Converter.
        /// </summary>
        public string Date { get; set; } = default!;

        /// <summary>Originale Spaltenüberschrift/Anzeige (z. B. "30.10.15").</summary>
        public string? Label { get; set; }

        /// <summary>Originaler Spaltenname im Dokument (falls abweichend von Label).</summary>
        public string? SourceColumn { get; set; }

        /// <summary>Alle Einzelwerte (Parameter) dieser Spalte.</summary>
        public List<Measurement> Measurements { get; set; } = new();
    }
}
