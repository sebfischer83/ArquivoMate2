using System.Text;
using System.Text.RegularExpressions;

namespace ArquivoMate2.Domain.Document
{
    public static class TitleNormalizer
    {
        private static readonly Regex MultiWhitespace = new(@"\s+", RegexOptions.Compiled);
        private const int MaxLen = 120;

        public static string FromFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return Fallback();
            var withoutExt = Path.GetFileNameWithoutExtension(fileName) ?? string.Empty;
            return Normalize(withoutExt);
        }

        public static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return Fallback();
            raw = raw.Replace('_', ' ').Replace('-', ' ');
            var sb = new StringBuilder();
            foreach (var c in raw)
            {
                if (!char.IsControl(c)) sb.Append(c);
            }
            var cleaned = sb.ToString().Trim();
            cleaned = MultiWhitespace.Replace(cleaned, " ");
            if (cleaned.Length == 0) return Fallback();
            if (cleaned.Length > MaxLen) cleaned = cleaned[..MaxLen];
            return cleaned;
        }

        private static string Fallback() => $"Dokument {Guid.NewGuid().ToString()[..6]}";
    }
}
