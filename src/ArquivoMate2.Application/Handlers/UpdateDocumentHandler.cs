using ArquivoMate2.Application.Commands;
using ArquivoMate2.Domain.Document;
using Marten;
using MediatR;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Dissolve;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Handlers
{
    /// <summary>
    /// Applies partial updates to document aggregates while validating field constraints and payload types.
    /// </summary>
    public class UpdateDocumentHandler : IRequestHandler<UpdateDocumentCommand, PatchResult>
    {
        private readonly IDocumentSession _session;
        private readonly ILogger<UpdateDocumentHandler> _logger;
        private static readonly string[] DisallowedProperties = { "Id", "UserId", "FilePath", "ThumbnailPath", "MetadataPath",
        "PreviewPath", "OccurredOn" };

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateDocumentHandler"/> class.
        /// </summary>
        /// <param name="session">Document session used to append events.</param>
        /// <param name="logger">Logger for diagnostic messages.</param>
        public UpdateDocumentHandler(IDocumentSession session, ILogger<UpdateDocumentHandler> logger)
        {
            _session = session;
            _logger = logger;
        }

        /// <summary>
        /// Validates and applies document field updates, emitting a <see cref="DocumentUpdated"/> event when successful.
        /// </summary>
        /// <param name="request">Command containing the document identifier and the modified fields.</param>
        /// <param name="cancellationToken">Cancellation token propagated from the caller.</param>
        /// <returns>The result of the patch operation.</returns>
        public async Task<PatchResult> Handle(UpdateDocumentCommand request, CancellationToken cancellationToken)
        {
            var doc = await _session.Events.AggregateStreamAsync<Document>(request.DocumentId);

            var changes = new Dictionary<string, object>();

            if (request.Dto == null || request.Dto.Fields == null)
            {
                _logger.LogError("No fields provided for update.");
                return PatchResult.Invalid;
            }

            _logger.LogInformation("Starting update for document {documentId}. {count} properties will be changed.", request.DocumentId, request.Dto.Fields.Count);

            // Detect disallowed properties with case-insensitive comparison
            var disallowed = request.Dto.Fields.Keys.Where(k => DisallowedProperties.Any(d => string.Equals(d, k, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (disallowed.Any())
            {
                _logger.LogError("Disallowed properties attempted to update: {@disallowed}", disallowed);
                return PatchResult.Forbidden;
            }

            // Translate the incoming dictionary into strongly typed values compatible with the aggregate
            foreach (var kvp in request.Dto.Fields)
            {
                // Perform a case-insensitive property lookup on the Document type
                var propertyInfo = typeof(Document).GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (propertyInfo == null)
                {
                    _logger.LogError("Property {property} doesnt exist on {document}.", kvp.Key, nameof(Document));
                    return PatchResult.Invalid;
                }
                try
                {
                    var targetType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;
                    object? value = null;
                    if (kvp.Value == null)
                    {
                        value = null;
                    }
                    else if (targetType == typeof(string))
                    {
                        // Validate that the payload is truly a string and not an array/object
                        if (kvp.Value is string s)
                        {
                            value = s;
                        }
                        else if (kvp.Value is System.Text.Json.JsonElement je && je.ValueKind == JsonValueKind.String)
                        {
                            value = je.GetString();
                        }
                        else
                        {
                            _logger.LogError("Type mismatch: Property {property} expects a string but got {type}.", kvp.Key, kvp.Value.GetType().Name);
                            return PatchResult.Invalid;
                        }
                    }
                    else
                    {
                        // Additional guard: reject arrays when the target type is not a collection
                        if (kvp.Value is System.Text.Json.JsonElement je && je.ValueKind == JsonValueKind.Array && !targetType.IsArray && !(targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>)))
                        {
                            _logger.LogError("Type mismatch: Property {property} expects {targetType} but got array.", kvp.Key, targetType.Name);
                            return PatchResult.Invalid;
                        }
                        // Serialize + deserialize as a general-purpose conversion when the JSON payload matches the target type
                        var json = JsonSerializer.Serialize(kvp.Value);
                        value = JsonSerializer.Deserialize(json, targetType);
                    }

                    // Use the canonical property name from PropertyInfo to ensure consistent casing in events/projections
                    changes.Add(propertyInfo.Name, value!);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error");
                    return PatchResult.Failed;
                }
            }
            var @event = new DocumentUpdated(request.DocumentId, changes, DateTime.UtcNow);
            _session.Events.Append(request.DocumentId, @event);
            await _session.SaveChangesAsync();

            return PatchResult.Success;
        }
    }
}
