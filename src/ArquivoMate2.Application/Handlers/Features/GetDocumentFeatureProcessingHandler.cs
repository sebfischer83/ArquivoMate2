using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Marten;
using ArquivoMate2.Application.Queries.Features;
using ArquivoMate2.Shared.Models;
using ArquivoMate2.Domain.Features;
using System.Linq;
using System;

namespace ArquivoMate2.Application.Handlers.Features
{
    public class GetDocumentFeatureProcessingHandler : IRequestHandler<GetDocumentFeatureProcessingQuery, DocumentFeatureProcessingDto?>
    {
        private readonly IQuerySession _query;
        private readonly ArquivoMate2.Application.Interfaces.ICurrentUserService _currentUserService;
        private readonly ArquivoMate2.Application.Interfaces.IDocumentAccessService _documentAccessService;

        public GetDocumentFeatureProcessingHandler(IQuerySession query, ArquivoMate2.Application.Interfaces.ICurrentUserService currentUserService, ArquivoMate2.Application.Interfaces.IDocumentAccessService documentAccessService)
        {
            _query = query;
            _currentUserService = currentUserService;
            _documentAccessService = documentAccessService;
        }

        public async Task<DocumentFeatureProcessingDto?> Handle(GetDocumentFeatureProcessingQuery request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId;
            var hasAccess = await _documentAccessService.HasAccessToDocumentAsync(request.DocumentId, userId, cancellationToken);
            if (!hasAccess) return null;

            var status = await _query.Query<DocumentFeatureProcessing>()
                .FirstOrDefaultAsync(x => x.DocumentId == request.DocumentId && x.FeatureKey == request.FeatureKey, cancellationToken);

            if (status == null) return null;

            // Map domain enum to shared DTO enum
            ArquivoMate2.Shared.Models.FeatureProcessingState sharedState;
            if (!Enum.TryParse<ArquivoMate2.Shared.Models.FeatureProcessingState>(status.State.ToString(), true, out sharedState))
            {
                sharedState = ArquivoMate2.Shared.Models.FeatureProcessingState.Pending;
            }

            return new DocumentFeatureProcessingDto
            {
                Id = status.Id,
                DocumentId = status.DocumentId,
                FeatureKey = status.FeatureKey,
                State = sharedState,
                AttemptCount = status.AttemptCount,
                ChatBotAvailable = status.ChatBotAvailable,
                ChatBotUsed = status.ChatBotUsed,
                ChatBotCallCount = status.ChatBotCallCount,
                CreatedAtUtc = status.CreatedAtUtc,
                StartedAtUtc = status.StartedAtUtc,
                CompletedAtUtc = status.CompletedAtUtc,
                FailedAtUtc = status.FailedAtUtc,
                LastError = status.LastError
            };
        }
    }
}
