using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ArquivoMate2.Infrastructure.Services.Llm
{
    public static class OpenAIHelper
    {
        public static string BuildSchemaJson(IEnumerable<string> documentTypes)
        {
            var types = documentTypes?
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            var properties = new Dictionary<string, object?>
            {
                ["date"] = new Dictionary<string, object?> { ["type"] = "string" },
                ["documentType"] = BuildDocumentTypeSchema(types),
                ["sender"] = BuildPartySchema(),
                ["recipient"] = BuildPartySchema(),
                ["customerNumber"] = new Dictionary<string, object?> { ["type"] = "string" },
                ["invoiceNumber"] = new Dictionary<string, object?> { ["type"] = "string" },
                ["totalPrice"] = new Dictionary<string, object?> { ["type"] = new[] { "number", "null" } },
                ["title"] = new Dictionary<string, object?> { ["type"] = "string" },
                ["keywords"] = new Dictionary<string, object?>
                {
                    ["type"] = "array",
                    ["items"] = new Dictionary<string, object?> { ["type"] = "string" }
                },
                ["summary"] = new Dictionary<string, object?> { ["type"] = "string" }
            };

            var schema = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = new[]
                {
                    "date",
                    "documentType",
                    "sender",
                    "recipient",
                    "customerNumber",
                    "invoiceNumber",
                    "totalPrice",
                    "title",
                    "keywords",
                    "summary"
                },
                ["additionalProperties"] = false
            };

            return JsonSerializer.Serialize(schema);
        }

        private static Dictionary<string, object?> BuildDocumentTypeSchema(IReadOnlyList<string> types)
        {
            var schema = new Dictionary<string, object?>
            {
                ["type"] = "string"
            };

            if (types.Count > 0)
            {
                schema["enum"] = types;
            }

            return schema;
        }

        private static Dictionary<string, object?> BuildPartySchema()
        {
            return new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["firstName"] = new Dictionary<string, object?> { ["type"] = "string" },
                    ["lastName"] = new Dictionary<string, object?> { ["type"] = "string" },
                    ["companyName"] = new Dictionary<string, object?> { ["type"] = "string" },
                    ["street"] = new Dictionary<string, object?> { ["type"] = "string" },
                    ["houseNumber"] = new Dictionary<string, object?> { ["type"] = "string" },
                    ["postalCode"] = new Dictionary<string, object?> { ["type"] = "string" },
                    ["city"] = new Dictionary<string, object?> { ["type"] = "string" }
                },
                ["required"] = new[]
                {
                    "firstName",
                    "lastName",
                    "companyName",
                    "street",
                    "houseNumber",
                    "postalCode",
                    "city"
                },
                ["additionalProperties"] = false
            };
        }
    }
}
