namespace ArquivoMate2.Application.Features.Processors.LabResults.Domain.Parsing
{
    public sealed class Measurement
    {
        public string Parameter { get; set; } = default!;
        public string Result { get; set; } = default!;
        public string? Unit { get; set; }
        public string? Reference { get; set; }
    }
}
