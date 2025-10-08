using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Queries.Documents;
using ArquivoMate2.Domain.ReadModels;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Documents;

public class GetDocumentStatsQueryHandler : IRequestHandler<GetDocumentStatsQuery, DocumentStatsQueryResultDto>
{
    private readonly IQuerySession _querySession;
    private readonly ISearchClient _searchClient;

    public GetDocumentStatsQueryHandler(IQuerySession querySession, ISearchClient searchClient)
    {
        _querySession = querySession;
        _searchClient = searchClient;
    }

    public async Task<DocumentStatsQueryResultDto> Handle(GetDocumentStatsQuery request, CancellationToken cancellationToken)
    {
        var sharedAccessibleIds = await _querySession.Query<DocumentAccessView>()
            .Where(a => a.EffectiveUserIds.Contains(request.UserId) && a.OwnerUserId != request.UserId)
            .Select(a => a.Id)
            .ToListAsyncFallback(cancellationToken);

        var sharedSet = sharedAccessibleIds.Count > 0 ? new HashSet<Guid>(sharedAccessibleIds) : new HashSet<Guid>();

        var accessibleQuery = _querySession.Query<DocumentView>()
            .Where(d => !d.Deleted && (d.UserId == request.UserId || sharedSet.Contains(d.Id)));

        var totalCount = await accessibleQuery.CountAsyncFallback(cancellationToken);
        var notAccepted = await accessibleQuery.Where(d => !d.Accepted).CountAsyncFallback(cancellationToken);
        var characters = await accessibleQuery.SumAsyncFallback(d => d.ContentLength, cancellationToken);

        var facets = await _searchClient.GetFacetsAsync(request.UserId, cancellationToken);

        return new DocumentStatsQueryResultDto
        {
            Documents = totalCount,
            NotAccepted = notAccepted,
            Characters = characters,
            Facets = facets
        };
    }
}
