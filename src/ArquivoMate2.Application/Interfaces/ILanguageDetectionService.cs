namespace ArquivoMate2.Application.Interfaces
{
    public interface ILanguageDetectionService
    {
        Task<(string? IsoCode, string? TesseractCode)> DetectFromPdfAsync(Stream pdfStream, CancellationToken ct = default);
        Task<(string? IsoCode, string? TesseractCode)> DetectFromImageOrPdfAsync(Stream fileStream, string[] candidateTesseract, CancellationToken ct = default);
    }
}
