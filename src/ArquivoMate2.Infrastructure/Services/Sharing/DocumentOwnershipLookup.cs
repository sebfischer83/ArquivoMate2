using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Interfaces.Sharing;
using ArquivoMate2.Domain.ReadModels;
using Marten;

namespace ArquivoMate2.Infrastructure.Services.Sharing;

/// <summary>
/// Marten backed implementation for looking up document ownership.
/// </summary>
internal sealed class DocumentOwnershipLookup : IDocumentOwnershipLookup
{
    private readonly IQuerySession _querySession;

    public DocumentOwnershipLookup(IQuerySession querySession)
    {
        _querySession = querySession;
    }

    public async Task<DocumentOwnerInfo?> GetAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var result = await _querySession.Query<DocumentView>()
            .Where(d => d.Id == documentId)
            .Select(d => new { d.Id, d.UserId, d.Deleted })
            .FirstOrDefaultAsync(cancellationToken);

        if (result is null)
            return null;

        return new DocumentOwnerInfo(result.Id, result.UserId, result.Deleted);
    }
}
