using ArquivoMate2.Application.Interfaces;
using ImageMagick;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using Lingua;

namespace ArquivoMate2.Infrastructure.Services
{
    public class LanguageDetectionService : ILanguageDetectionService
    {
        private readonly ILogger<LanguageDetectionService> _logger;

        // ISO2 -> Tesseract
        private static readonly Dictionary<string, string> Iso2ToTesseract = new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"]="eng",
            ["de"]="deu",
            ["fr"]="fra",
            ["es"]="spa",
            ["it"]="ita",
            ["pt"]="por",
            ["nl"]="nld",
            ["pl"]="pol",
            ["tr"]="tur",
            ["cs"]="ces",
            ["ru"]="rus"
        };

        // Language enum -> ISO2 mapping (Lingua Language enum values)
        private static readonly Dictionary<Language, string> LanguageToIso2 = new()
        {
            [Language.English] = "en",
            [Language.German] = "de",
            [Language.French] = "fr",
            [Language.Spanish] = "es",
            [Language.Italian] = "it",
            [Language.Portuguese] = "pt",
            [Language.Dutch] = "nl",
            [Language.Polish] = "pl",
            [Language.Turkish] = "tr",
            [Language.Czech] = "cs",
            [Language.Russian] = "ru"
        };

        private static readonly Lazy<LanguageDetector> Detector = new(() =>
            LanguageDetectorBuilder
                .FromAllSpokenLanguages().WithPreloadedLanguageModels()
                .Build());

        private const int MinSampleLength = 60;
        private const double MinConfidence = 0.50;
        private const double MinDelta = 0.15;
        private const int MaxChars = 4000;

        public LanguageDetectionService(ILogger<LanguageDetectionService> logger)
        {
            _logger = logger;
        }

        public Task<(string? IsoCode, string? TesseractCode)> DetectFromPdfAsync(Stream pdfStream, CancellationToken ct = default)
        {
            try
            {
                var sample = ExtractPdfSample(pdfStream, MaxChars);
                var result = Detect(sample);
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PDF language detection failed");
                return Task.FromResult<(string?, string?)>((null, null));
            }
        }

        public async Task<(string? IsoCode, string? TesseractCode)> DetectFromImageOrPdfAsync(Stream fileStream, string[] candidateTesseract, CancellationToken ct = default)
        {
            try
            {
                fileStream.Position = 0;
                using var images = new MagickImageCollection();
                images.Read(fileStream, new MagickReadSettings { Density = new Density(120) });
                if (images.Count == 0) return (null, null);

                using var first = (MagickImage)images.First().Clone();
                first.Format = MagickFormat.Png;
                var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
                first.Write(tmp);
                try
                {
                    var joined = BuildProbeLanguageList(candidateTesseract);
                    var psi = new System.Diagnostics.ProcessStartInfo("tesseract", $"{tmp} stdout -l {joined}")
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = System.Diagnostics.Process.Start(psi)!;
                    string ocr = await proc.StandardOutput.ReadToEndAsync();
                    await proc.WaitForExitAsync(ct);
                    if (proc.ExitCode != 0) return (null, null);

                    var sample = Truncate(ocr, MaxChars);
                    return Detect(sample);
                }
                finally { try { File.Delete(tmp); } catch { } }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Image-based language detection failed");
                return (null, null);
            }
        }

        private (string? IsoCode, string? TesseractCode) Detect(string sample)
        {
            if (string.IsNullOrWhiteSpace(sample)) return (null, null);
            sample = Normalize(sample);
            if (sample.Length < MinSampleLength) return (null, null);

            try
            {
                var confidenceValues = Detector.Value
                    .ComputeLanguageConfidenceValues(sample)
                    .OrderByDescending(c => c.Value)
                    .ToList();
                if (confidenceValues.Count == 0) return (null, null);

                var best = confidenceValues[0];
                var hasSecond = confidenceValues.Count > 1;
                if (best.Value < MinConfidence) return (null, null);
                if (hasSecond && (best.Value - confidenceValues[1].Value) < MinDelta) return (null, null);

                if (!LanguageToIso2.TryGetValue(best.Key, out var iso2)) return (null, null);
                var tess = Iso2ToTesseract.TryGetValue(iso2, out var t) ? t : "eng";
                return (iso2, tess);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lingua detection failed");
                return (null, null);
            }
        }

        private static string BuildProbeLanguageList(string[] candidate)
        {
            if (candidate != null && candidate.Length > 0)
            {
                var valid = candidate.Where(c => Iso2ToTesseract.Values.Contains(c, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (valid.Count > 0) return string.Join('+', valid);
            }
            return string.Join('+', Iso2ToTesseract.Values.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static string Normalize(string input)
        {
            var collapsed = System.Text.RegularExpressions.Regex.Replace(input, @"\s+", " ");
            return collapsed.Trim();
        }

        private static string Truncate(string input, int max) => input.Length <= max ? input : input.Substring(0, max);

        private string ExtractPdfSample(Stream pdfStream, int maxChars)
        {
            pdfStream.Position = 0;
            using var doc = PdfDocument.Open(pdfStream);
            var sb = new System.Text.StringBuilder();
            foreach (var page in doc.GetPages())
            {
                if (sb.Length >= maxChars) break;
                sb.Append(page.Text);
                if (sb.Length >= maxChars) break;
            }
            return Truncate(sb.ToString(), maxChars);
        }
    }
}
