using ArquivoMate2.Application.Commands.Parties;
using ArquivoMate2.Application.Models;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Parties;

public class DeletePartyHandler : IRequestHandler<DeletePartyCommand, bool>
{
    private readonly IDocumentSession _session;

    public DeletePartyHandler(IDocumentSession session)
    {
        _session = session;
    }

    public async Task<bool> Handle(DeletePartyCommand request, CancellationToken cancellationToken)
    {
        var party = await _session.LoadAsync<PartyInfo>(request.Id, cancellationToken);
        if (party is null) return false;

        _session.Delete(party);
        await _session.SaveChangesAsync(cancellationToken);
        return true;
    }
}
