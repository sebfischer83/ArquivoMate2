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

            _logger.LogInformation("Starting update for document {documentId}. {count} properties will be changed.", request.DocumentId, request.Json.Properties().Count());

            var disallowed = request.Json.Properties().Where(p => DisallowedProperties.Contains(p.Name)).Select(x => x.Name).ToArray();
            if (disallowed != null && disallowed.Any())
            {
                _logger.LogError("Disallowed properties attempted to update: {@disallowed}", disallowed);
                return PatchResult.Forbidden;
            }

            foreach (var prop in request.Json.Properties())
            {
                var propertyInfo = typeof(Document).GetProperty(prop.Name);
                if (propertyInfo == null)
                {
                    _logger.LogError("Property {property} doesnt exist on {document}.", prop.Name, nameof(Document));
                    return PatchResult.Invalid;
                }
                try
                {
                    var targetType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;
                    var value = prop.Value.ToObject(targetType);
                    changes.Add(prop.Name, value);
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
