using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ArquivoMate2.API.Swagger
{
    /// <summary>
    /// Adds multipart/form-data request body descriptions for actions that accept IFormFile parameters
    /// or DTOs that contain IFormFile properties.
    /// </summary>
    public class FormFileOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation == null || context?.MethodInfo == null)
                return;

            var fileFields = new List<(string Name, bool IsArray)>();

            // Inspect method parameters: direct IFormFile parameters
            var methodParams = context.MethodInfo.GetParameters();
            foreach (var p in methodParams)
            {
                if (p.ParameterType == typeof(IFormFile))
                {
                    fileFields.Add((p.Name, false));
                }
                else if (typeof(IEnumerable<IFormFile>).IsAssignableFrom(p.ParameterType))
                {
                    fileFields.Add((p.Name, true));
                }
                else
                {
                    // Inspect complex parameter properties for IFormFile members
                    var props = p.ParameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var prop in props)
                    {
                        if (prop.PropertyType == typeof(IFormFile))
                        {
                            fileFields.Add((prop.Name, false));
                        }
                        else if (typeof(IEnumerable<IFormFile>).IsAssignableFrom(prop.PropertyType))
                        {
                            fileFields.Add((prop.Name, true));
                        }
                    }
                }
            }

            if (!fileFields.Any())
                return;

            // Remove any parameters that correspond to file fields (they will be part of the request body)
            if (operation.Parameters != null && operation.Parameters.Any())
            {
                foreach (var ff in fileFields)
                {
                    var toRemove = operation.Parameters.FirstOrDefault(p => p.Name == ff.Name);
                    if (toRemove != null) operation.Parameters.Remove(toRemove);
                }
            }

            // Build multipart/form-data schema with binary properties for files
            var schema = new OpenApiSchema
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchema>()
            };

            foreach (var ff in fileFields)
            {
                if (!ff.IsArray)
                {
                    schema.Properties[ff.Name] = new OpenApiSchema { Type = "string", Format = "binary" };
                }
                else
                {
                    schema.Properties[ff.Name] = new OpenApiSchema
                    {
                        Type = "array",
                        Items = new OpenApiSchema { Type = "string", Format = "binary" }
                    };
                }
            }

            operation.RequestBody = operation.RequestBody ?? new OpenApiRequestBody();
            operation.RequestBody.Content["multipart/form-data"] = new OpenApiMediaType { Schema = schema };
        }
    }
}
