using ArquivoMate2.Application.Interfaces;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace ArquivoMate2.Application.Features.Processors.LabResults.Services
{
    public class DefaultParameterNormalizer : IParameterNormalizer
    {
        private static readonly Regex MultiWhitespace = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex RemoveParentheses = new(@"\([^)]*\)", RegexOptions.Compiled);

        public string Normalize(string parameter)
        {
            if (string.IsNullOrWhiteSpace(parameter)) return string.Empty;
            var work = parameter.ToLowerInvariant().Trim();
            work = RemoveParentheses.Replace(work, " ");

            // remove diacritics
            var formD = work.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in formD)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            work = sb.ToString().Normalize(NormalizationForm.FormC);

            // normalize micro symbols
            work = work.Replace('µ', 'u').Replace('?', 'u');

            // collapse whitespace
            work = MultiWhitespace.Replace(work, " ").Trim();

            return work;
        }
    }
}
