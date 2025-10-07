using System;
using System.Collections.Generic;

namespace ArquivoMate2.API.Utilities
{
    public static class RedisInfoParser
    {
        // Parses Redis INFO output into section -> (key -> value) dictionary
        public static Dictionary<string, Dictionary<string, object?>> Parse(string info)
        {
            var result = new Dictionary<string, Dictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(info)) return result;

            string? currentSection = null;
            var lines = info.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;

                if (line.StartsWith("#"))
                {
                    currentSection = line.TrimStart('#').Trim();
                    if (!result.ContainsKey(currentSection))
                        result[currentSection] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    continue;
                }

                if (currentSection == null)
                {
                    currentSection = "General";
                    if (!result.ContainsKey(currentSection))
                        result[currentSection] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                }

                var idx = line.IndexOf(':');
                if (idx <= 0)
                {
                    // fallback: store whole line
                    result[currentSection][line] = line;
                    continue;
                }

                var key = line.Substring(0, idx).Trim();
                var val = line.Substring(idx + 1).Trim();

                // Special-case keyspace entries like: db0:keys=1,expires=0,avg_ttl=0
                if (currentSection.Equals("Keyspace", StringComparison.OrdinalIgnoreCase) && key.StartsWith("db", StringComparison.OrdinalIgnoreCase))
                {
                    var inner = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    var parts = val.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        var kv = part.Split('=', 2);
                        if (kv.Length == 2)
                        {
                            var ik = kv[0].Trim();
                            var iv = TryParseValue(kv[1].Trim());
                            inner[ik] = iv;
                        }
                    }
                    result[currentSection][key] = inner;
                    continue;
                }

                // Generic value parsing
                result[currentSection][key] = TryParseValue(val);
            }

            return result;
        }

        private static object? TryParseValue(string val)
        {
            if (string.IsNullOrEmpty(val)) return val;

            // Try boolean
            if (string.Equals(val, "yes", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(val, "no", StringComparison.OrdinalIgnoreCase)) return false;

            // Try integer
            if (long.TryParse(val, out var l)) return l;

            // Try double
            if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;

            // Fallback to string
            return val;
        }
    }
}
