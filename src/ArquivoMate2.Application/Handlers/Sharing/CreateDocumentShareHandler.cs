using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Commands.Sharing;
using ArquivoMate2.Domain.Sharing;
using ArquivoMate2.Shared.Models.Sharing; // ShareTargetType
using Marten;
using MediatR;
using ArquivoMate2.Application.Interfaces.Sharing; // abstraction ports

namespace ArquivoMate2.Application.Handlers.Sharing;

public class CreateDocumentShareHandler : IRequestHandler<CreateDocumentShareCommand, DocumentShareDto>
{
    private readonly IDocumentSession _session;
    private readonly IQuerySession _querySession;
    private readonly IDocumentOwnershipLookup _ownershipLookup; // ownership lookup abstraction
    private readonly IDocumentAccessUpdater _accessUpdater; // access projection updater

    public CreateDocumentShareHandler(IDocumentSession session, IQuerySession querySession, IDocumentOwnershipLookup ownershipLookup, IDocumentAccessUpdater accessUpdater)
    {
        _session = session;
        _querySession = querySession;
        _ownershipLookup = ownershipLookup;
        _accessUpdater = accessUpdater;
    }

    public async Task<DocumentShareDto> Handle(CreateDocumentShareCommand request, CancellationToken cancellationToken)
    {
        if (request.Target is null || string.IsNullOrWhiteSpace(request.Target.Identifier))
        {
            throw new ArgumentException("Share target is required", nameof(request.Target));
        }

        var documentInfo = await _ownershipLookup.GetAsync(request.DocumentId, cancellationToken);

        if (documentInfo is null || documentInfo.Value.Deleted || !string.Equals(documentInfo.Value.UserId, request.OwnerUserId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Document not found or access denied.");
        }

        if (request.Target.Type == ShareTargetType.User &&
            string.Equals(request.Target.Identifier, request.OwnerUserId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Cannot share a document with yourself.");
        }

        if (request.Target.Type == ShareTargetType.Group)
        {
            var group = await _querySession.Query<ShareGroup>()
                .Where(g => g.Id == request.Target.Identifier)
                .FirstOrDefaultAsync(cancellationToken);

            if (group is null || !string.Equals(group.OwnerUserId, request.OwnerUserId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Group not found or access denied.");
            }
        }

        var exists = await _querySession.Query<DocumentShare>()
            .Where(s => s.DocumentId == request.DocumentId && s.Target.Type == request.Target.Type && s.Target.Identifier == request.Target.Identifier)
            .AnyAsync(cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("Document is already shared with the selected target.");
        }

        var share = new DocumentShare
        {
            DocumentId = request.DocumentId,
            OwnerUserId = request.OwnerUserId,
            Target = new ShareTarget
            {
                Type = request.Target.Type,
                Identifier = request.Target.Identifier
            },
            SharedAt = DateTime.UtcNow,
            GrantedBy = request.OwnerUserId
        };

        _session.Store(share);
        await _session.SaveChangesAsync(cancellationToken);

        await _accessUpdater.AddShareAsync(share, cancellationToken);

        return new DocumentShareDto
        {
            Id = share.Id,
            DocumentId = share.DocumentId,
            Target = share.Target,
            SharedAt = share.SharedAt,
            GrantedBy = share.GrantedBy
        };
    }
}
