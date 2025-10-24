using ArquivoMate2.Application.Interfaces;
using System.Collections.Generic;
using System;

namespace ArquivoMate2.Application.Features.Processors.LabResults.Services
{
    public class DefaultUnitNormalizer : IUnitNormalizer
    {
        private static readonly Dictionary<string, string> s_mappings = new(StringComparer.OrdinalIgnoreCase)
        {
            { "g/l", "g/L" },
            { "g\\l", "g/L" },
            { "g/liter", "g/L" },
            { "g/dl", "g/dL" },
            { "g\\dl", "g/dL" },
            { "g/deciliter", "g/dL" },
            { "mmol/l", "mmol/L" },
            { "mmol\\l", "mmol/L" },
            { "mmol/liter", "mmol/L" },
            { "mg/dl", "mg/dL" },
            { "mg\\dl", "mg/dL" },
            { "mg/deciliter", "mg/dL" },
            { "ng/ml", "ng/mL" },
            { "ng\\ml", "ng/mL" },
            { "pg/ml", "pg/mL" },
            { "ug/l", "痢/L" },
            { "ug/ml", "痢/mL" },
            { "mcg/l", "痢/L" },
            { "mcg/ml", "痢/mL" },
            { "iu/l", "IU/L" },
            { "iu\\l", "IU/L" },
            { "u/l", "U/L" },
            { "u\\l", "U/L" },
            { "cells/ul", "cells/無" },
            { "cells\\ul", "cells/無" },
            { "k/ul", "10^3/無" },
            { "k\\ul", "10^3/無" },
            { "10^9/l", "10^9/L" },
            { "10e9/l", "10^9/L" },
            { "%", "%" },
            { "", string.Empty }
        };

        public string Normalize(string unit)
        {
            if (string.IsNullOrWhiteSpace(unit)) return string.Empty;

            var key = unit.Trim().ToLowerInvariant();
            if (s_mappings.TryGetValue(key, out var mapped))
                return mapped;

            // default: return trimmed lowercase representation
            return key;
        }
    }
}
