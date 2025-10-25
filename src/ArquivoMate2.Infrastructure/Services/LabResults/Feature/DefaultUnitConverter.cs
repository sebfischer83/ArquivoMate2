using System;
using System.Collections.Generic;
using System.Globalization;
using ArquivoMate2.Application.Interfaces;

namespace ArquivoMate2.Infrastructure.Services.LabResults
{
    /// <summary>
    /// Lightweight unit converter with a few domain-specific conversions (e.g. mg/dL <-> mmol/L for common analytes).
    /// This is intentionally small and extendable.
    /// </summary>
    public class DefaultUnitConverter : IUnitConverter
    {
        private static readonly Dictionary<(string from, string to), Func<decimal, decimal>> s_converters = new()
        {
            // Glucose-ish: mg/dL to mmol/L: divide by 18.0
            {("mg/dl","mmol/l"), v => v / 18.0m },
            {("mmol/l","mg/dl"), v => v * 18.0m },

            // Cholesterol: mg/dL <-> mmol/L (factor 38.67)
            {("mg/dl","mmol/l:cholesterol"), v => v / 38.67m },
            {("mmol/l:cholesterol","mg/dl"), v => v * 38.67m },

            // simple case: micrograms/mL <-> ng/mL
            {("ug/ml","ng/ml"), v => v * 1000m },
            {("ng/ml","ug/ml"), v => v / 1000m }
        };

        private static string NormalizeUnit(string u) => (u ?? string.Empty).Trim().ToLowerInvariant();

        public bool TryConvert(decimal value, string fromUnit, string toUnit, out decimal converted)
        {
            converted = value;
            var f = NormalizeUnit(fromUnit);
            var t = NormalizeUnit(toUnit);

            // direct mapping
            if (s_converters.TryGetValue((f,t), out var conv))
            {
                converted = conv(value);
                return true;
            }

            // heuristics: handle qualifiers like "mmol/l" vs "mmol/l:cholesterol"
            if (t.StartsWith("mmol/l") && f == "mg/dl")
            {
                // try generic conversion factor 38.67 for cholesterol-like analytes if specific token present
                converted = value / 38.67m;
                return true;
            }

            // no conversion known
            return false;
        }

        public bool TryConvertRange(decimal? fromValue, decimal? toValue, string fromUnit, string toUnit, out decimal? convertedFrom, out decimal? convertedTo)
        {
            convertedFrom = null;
            convertedTo = null;

            var any = false;
            if (fromValue.HasValue && TryConvert(fromValue.Value, fromUnit, toUnit, out var cf))
            {
                convertedFrom = cf;
                any = true;
            }
            if (toValue.HasValue && TryConvert(toValue.Value, fromUnit, toUnit, out var ct))
            {
                convertedTo = ct;
                any = true;
            }

            return any;
        }
    }
}
