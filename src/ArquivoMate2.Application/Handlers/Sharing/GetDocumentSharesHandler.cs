using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Queries.Sharing;
using ArquivoMate2.Domain.Sharing;
using ArquivoMate2.Shared.Models.Sharing;
using Marten;
using MediatR;
using ArquivoMate2.Application.Interfaces.Sharing;

namespace ArquivoMate2.Application.Handlers.Sharing;

public class GetDocumentSharesHandler : IRequestHandler<GetDocumentSharesQuery, IReadOnlyCollection<DocumentShareDto>>
{
    private readonly IQuerySession _querySession;
    private readonly IDocumentOwnershipLookup _ownershipLookup;

    public GetDocumentSharesHandler(IQuerySession querySession, IDocumentOwnershipLookup ownershipLookup)
    {
        _querySession = querySession;
        _ownershipLookup = ownershipLookup;
    }

    public async Task<IReadOnlyCollection<DocumentShareDto>> Handle(GetDocumentSharesQuery request, CancellationToken cancellationToken)
    {
        var documentInfo = await _ownershipLookup.GetAsync(request.DocumentId, cancellationToken);

        if (documentInfo is null || documentInfo.Value.Deleted || !string.Equals(documentInfo.Value.UserId, request.OwnerUserId, StringComparison.Ordinal))
        {
            return Array.Empty<DocumentShareDto>();
        }

        var shares = await _querySession.Query<DocumentShare>()
            .Where(s => s.DocumentId == request.DocumentId)
            .OrderBy(s => s.SharedAt)
            .ToListAsync(cancellationToken);

        return shares.Select(s => new DocumentShareDto
        {
            Id = s.Id,
            DocumentId = s.DocumentId,
            Target = s.Target,
            SharedAt = s.SharedAt,
            GrantedBy = s.GrantedBy
        }).ToList();
    }
}
