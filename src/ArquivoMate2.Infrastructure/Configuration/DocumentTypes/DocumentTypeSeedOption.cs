using System.Collections.Generic;

namespace ArquivoMate2.Infrastructure.Configuration.DocumentTypes
{
    public class DocumentTypeSeedOption
    {
        public string Name { get; set; } = string.Empty;

        // New: allow multiple system features per seeded type
        public List<string>? SystemFeatures { get; set; }

        // Legacy: single system feature property still supported for existing appsettings.json
        public string? SystemFeature { get; set; }

        public List<string> GetSystemFeatures()
        {
            if (SystemFeatures != null && SystemFeatures.Count > 0) return SystemFeatures;
            if (!string.IsNullOrWhiteSpace(SystemFeature)) return new List<string> { SystemFeature! };
            return new List<string>();
        }
    }
}
