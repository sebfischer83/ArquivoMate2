using ArquivoMate2.Application.Interfaces;
using ImageMagick;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using Lingua;
using System.Collections.Generic;
using System.Linq;

namespace ArquivoMate2.Infrastructure.Services
{
    public class LanguageDetectionService : ILanguageDetectionService
    {
        private readonly ILogger<LanguageDetectionService> _logger;
        private readonly LanguageDetector _detector;

        // ISO2 -> Tesseract (traineddata) mapping. Values chosen from common Tesseract codes.
        // If a language code is not present here, fallback is 'eng'.
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
            ["ru"]="rus",
            // Extended languages
            ["af"]="afr",
            ["sq"]="sqi",
            ["ar"]="ara",
            ["hy"]="hye",
            ["az"]="aze",
            ["eu"]="eus",
            ["be"]="bel",
            ["bn"]="ben",
            ["nb"]="nob", // Norwegian Bokmål
            ["bs"]="bos",
            ["bg"]="bul",
            ["ca"]="cat",
            ["zh"]="chi_sim", // default simplified
            ["hr"]="hrv",
            ["da"]="dan",
            ["eo"]="epo",
            ["et"]="est",
            ["fi"]="fin",
            ["lg"]="lug",
            ["ka"]="kat", // Georgian modern
            ["el"]="ell",
            ["gu"]="guj",
            ["he"]="heb",
            ["hi"]="hin",
            ["hu"]="hun",
            ["is"]="isl",
            ["id"]="ind",
            ["ga"]="gle",
            ["ja"]="jpn",
            ["kk"]="kaz",
            ["ko"]="kor",
            ["la"]="lat",
            ["lv"]="lav",
            ["lt"]="lit",
            ["mk"]="mkd",
            ["ms"]="msa",
            ["mi"]="mri", // Maori (modern code)
            ["mr"]="mar",
            ["mn"]="mon",
            ["no"]="nor",
            ["fa"]="fas",
            ["pa"]="pan",
            ["ro"]="ron",
            ["sr"]="srp",
            ["sn"]="sna", // Shona (if missing, will fallback during runtime usage)
            ["sk"]="slk",
            ["sl"]="slv",
            ["so"]="som",
            ["st"]="sot",
            ["sw"]="swa",
            ["sv"]="swe",
            ["tl"]="tgl",
            ["ta"]="tam",
            ["te"]="tel",
            ["th"]="tha",
            ["ts"]="tso",
            ["tn"]="tsn",
            ["uk"]="ukr",
            ["ur"]="urd",
            ["vi"]="vie",
            ["cy"]="cym",
            ["xh"]="xho",
            ["yo"]="yor",
            ["zu"]="zul"
        };

        // Language enum -> ISO2 mapping (Lingua Language enum values)
        private static readonly Dictionary<Language, string> LanguageToIso2 = new()
        {
            // Existing core set
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
            [Language.Russian] = "ru",
            // Extended full list (ISO 639-1 where available)
            [Language.Afrikaans] = "af",
            [Language.Albanian] = "sq",
            [Language.Arabic] = "ar",
            [Language.Armenian] = "hy",
            [Language.Azerbaijani] = "az",
            [Language.Basque] = "eu",
            [Language.Belarusian] = "be",
            [Language.Bengali] = "bn",
            [Language.Bokmal] = "nb",
            [Language.Bosnian] = "bs",
            [Language.Bulgarian] = "bg",
            [Language.Catalan] = "ca",
            [Language.Chinese] = "zh",
            [Language.Croatian] = "hr",
            [Language.Danish] = "da",
            [Language.Esperanto] = "eo",
            [Language.Estonian] = "et",
            [Language.Finnish] = "fi",
            [Language.Ganda] = "lg",
            [Language.Georgian] = "ka",
            [Language.Greek] = "el",
            [Language.Gujarati] = "gu",
            [Language.Hebrew] = "he",
            [Language.Hindi] = "hi",
            [Language.Hungarian] = "hu",
            [Language.Icelandic] = "is",
            [Language.Indonesian] = "id",
            [Language.Irish] = "ga",
            [Language.Japanese] = "ja",
            [Language.Kazakh] = "kk",
            [Language.Korean] = "ko",
            [Language.Latin] = "la",
            [Language.Latvian] = "lv",
            [Language.Lithuanian] = "lt",
            [Language.Macedonian] = "mk",
            [Language.Malay] = "ms",
            [Language.Maori] = "mi",
            [Language.Marathi] = "mr",
            [Language.Mongolian] = "mn",
            [Language.Nynorsk] = "no",
            [Language.Persian] = "fa",
            [Language.Punjabi] = "pa",
            [Language.Romanian] = "ro",
            [Language.Serbian] = "sr",
            [Language.Shona] = "sn",
            [Language.Slovak] = "sk",
            [Language.Slovene] = "sl",
            [Language.Somali] = "so",
            [Language.Sotho] = "st",
            [Language.Swahili] = "sw",
            [Language.Swedish] = "sv",
            [Language.Tagalog] = "tl",
            [Language.Tamil] = "ta",
            [Language.Telugu] = "te",
            [Language.Thai] = "th",
            [Language.Tsonga] = "ts",
            [Language.Tswana] = "tn",
            [Language.Ukrainian] = "uk",
            [Language.Urdu] = "ur",
            [Language.Vietnamese] = "vi",
            [Language.Welsh] = "cy",
            [Language.Xhosa] = "xh",
            [Language.Yoruba] = "yo",
            [Language.Zulu] = "zu"
        };

        private const int MinSampleLength = 60;
        private const double MinConfidence = 0.50;
        private const double MinDelta = 0.15;
        private const int MaxChars = 4000;

        public LanguageDetectionService(ILogger<LanguageDetectionService> logger, LanguageDetectionOptions options)
        {
            _logger = logger;
            var supported = new List<Language>();
            if (options?.SupportedLanguages != null && options.SupportedLanguages.Length > 0)
            {
                foreach (var code in options.SupportedLanguages)
                {
                    if (string.IsNullOrWhiteSpace(code)) continue;
                    var match = LanguageToIso2.FirstOrDefault(x => x.Value.Equals(code, System.StringComparison.OrdinalIgnoreCase));
                    if (!match.Equals(default(KeyValuePair<Language, string>)))
                    {
                        supported.Add(match.Key);
                    }
                    else
                    {
                        _logger.LogWarning("Language '{Lang}' is not supported and will be ignored for detection.", code);
                    }
                }
            }
            if (supported.Count == 0)
            {
                // Fallback auf Deutsch/Englisch
                supported.Add(Language.German);
                supported.Add(Language.English);
                _logger.LogWarning("No valid languages configured for detection. Falling back to German and English.");
            }
            _detector = LanguageDetectorBuilder.FromLanguages(supported.ToArray()).WithPreloadedLanguageModels().Build();
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
                var confidenceValues = _detector
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
                var valid = candidate.Where(c => Iso2ToTesseract.Values.Contains(c, System.StringComparer.OrdinalIgnoreCase)).Distinct(System.StringComparer.OrdinalIgnoreCase).ToList();
                if (valid.Count > 0) return string.Join('+', valid);
            }
            return string.Join('+', Iso2ToTesseract.Values.Distinct(System.StringComparer.OrdinalIgnoreCase));
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
