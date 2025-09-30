using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Commands.Sharing;
using ArquivoMate2.Domain.Sharing;
using ArquivoMate2.Shared.Models.Sharing;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Sharing;

public class UpdateShareGroupHandler : IRequestHandler<UpdateShareGroupCommand, ShareGroupDto?>
{
    private readonly IDocumentSession _session;

    public UpdateShareGroupHandler(IDocumentSession session)
    {
        _session = session;
    }

    public async Task<ShareGroupDto?> Handle(UpdateShareGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await _session.LoadAsync<ShareGroup>(request.GroupId, cancellationToken);
        if (group is null || !string.Equals(group.OwnerUserId, request.OwnerUserId, StringComparison.Ordinal))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            group.Name = request.Name.Trim();
        }

        group.MemberUserIds = request.MemberUserIds?.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();

        _session.Store(group);
        await _session.SaveChangesAsync(cancellationToken);

        return new ShareGroupDto
        {
            Id = group.Id,
            Name = group.Name,
            MemberUserIds = group.MemberUserIds
        };
    }
}
