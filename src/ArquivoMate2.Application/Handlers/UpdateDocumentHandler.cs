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
    public class UpdateDocumentHandler : IRequestHandler<UpdateDocumentCommand, PatchResult>
    {
        private readonly IDocumentSession _session;
        private readonly ILogger<UpdateDocumentHandler> _logger;
        private static readonly string[] DisallowedProperties = { "Id", "UserId", "FilePath", "ThumbnailPath", "MetadataPath",
        "PreviewPath", "OccurredOn" };

        public UpdateDocumentHandler(IDocumentSession session, ILogger<UpdateDocumentHandler> logger)
        {
            _session = session;
            _logger = logger;
        }

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

            var disallowed = request.Dto.Fields.Keys.Where(k => DisallowedProperties.Contains(k)).ToArray();
            if (disallowed.Any())
            {
                _logger.LogError("Disallowed properties attempted to update: {@disallowed}", disallowed);
                return PatchResult.Forbidden;
            }

            foreach (var kvp in request.Dto.Fields)
            {
                var propertyInfo = typeof(Document).GetProperty(kvp.Key);
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
                        // Robust: Prüfe, ob der Wert wirklich ein String ist und kein Array/Objekt
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
                        // Zusätzliche Prüfung: Wenn Ziel kein Array/List ist, aber Wert ein Array, Fehler
                        if (kvp.Value is System.Text.Json.JsonElement je && je.ValueKind == JsonValueKind.Array && !targetType.IsArray && !(targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>)))
                        {
                            _logger.LogError("Type mismatch: Property {property} expects {targetType} but got array.", kvp.Key, targetType.Name);
                            return PatchResult.Invalid;
                        }
                        var json = JsonSerializer.Serialize(kvp.Value);
                        value = JsonSerializer.Deserialize(json, targetType);
                    }
                    changes.Add(kvp.Key, value);
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
