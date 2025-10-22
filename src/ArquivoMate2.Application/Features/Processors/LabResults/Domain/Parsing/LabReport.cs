namespace ArquivoMate2.Application.Features.Processors.LabResults.Domain.Parsing
{
    public sealed class LabReport
    {
        public string LabName { get; set; } = default!;
        public string Patient { get; set; } = default!;

        /// <summary>
        /// Eine Liste von Spalten (z. B. verschiedene Erhebungsdaten).
        /// </summary>
        public List<ValueColumn> Values { get; set; } = new();
    }
}
