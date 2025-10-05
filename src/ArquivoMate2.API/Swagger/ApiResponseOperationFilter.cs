using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ArquivoMate2.API.Swagger
{
    /// <summary>
    /// Wraps successful JSON responses in the generic ApiResponse shape for OpenAPI documentation.
    /// Leaves ProblemDetails / ValidationProblemDetails untouched.
    /// </summary>
    public class ApiResponseOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation == null || operation.Responses == null)
                return;

            // Iterate a snapshot to allow modifying the collection
            foreach (var kv in operation.Responses.ToList())
            {
                var statusCode = kv.Key;
                var response = kv.Value;

                // Only wrap successful 2xx responses
                if (string.IsNullOrEmpty(statusCode) || statusCode.Length == 0 || statusCode[0] != '2')
                    continue;

                if (response.Content == null)
                    continue;

                if (!response.Content.TryGetValue("application/json", out var mediaType))
                    continue;

                var schema = mediaType.Schema;
                if (schema == null)
                    continue;

                // If the schema already looks like ApiResponse (contains success) or is ProblemDetails, skip
                if (schema.Properties != null && schema.Properties.ContainsKey("success"))
                    continue;
                if (schema.Properties != null && schema.Properties.ContainsKey("title") && schema.Properties.ContainsKey("status"))
                    continue; // likely ProblemDetails

                // Build wrapper schema with the original schema as `data`
                var wrapper = new OpenApiSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, OpenApiSchema>
                    {
                        ["success"] = new OpenApiSchema { Type = "boolean" },
                        ["message"] = new OpenApiSchema { Type = "string", Nullable = true },
                        ["errorCode"] = new OpenApiSchema { Type = "string", Nullable = true },
                        ["errors"] = new OpenApiSchema { Type = "object", Nullable = true, AdditionalProperties = new OpenApiSchema { Type = "array", Items = new OpenApiSchema { Type = "string" } } },
                        ["timestamp"] = new OpenApiSchema { Type = "string", Format = "date-time" },
                        ["data"] = schema
                    }
                };

                // Replace the schema in-place
                response.Content["application/json"].Schema = wrapper;
            }
        }
    }
}
