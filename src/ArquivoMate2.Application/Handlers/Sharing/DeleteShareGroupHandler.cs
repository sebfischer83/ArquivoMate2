using System;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Commands.Sharing;
using ArquivoMate2.Domain.Sharing;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Sharing;

public class DeleteShareGroupHandler : IRequestHandler<DeleteShareGroupCommand, bool>
{
    private readonly IDocumentSession _session;

    public DeleteShareGroupHandler(IDocumentSession session)
    {
        _session = session;
    }

    public async Task<bool> Handle(DeleteShareGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await _session.LoadAsync<ShareGroup>(request.GroupId, cancellationToken);
        if (group is null || !string.Equals(group.OwnerUserId, request.OwnerUserId, StringComparison.Ordinal))
        {
            return false;
        }

        _session.Delete<ShareGroup>(group.Id);
        await _session.SaveChangesAsync(cancellationToken);
        return true;
    }
}
