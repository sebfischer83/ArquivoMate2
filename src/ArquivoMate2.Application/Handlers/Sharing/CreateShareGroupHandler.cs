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

public class CreateShareGroupHandler : IRequestHandler<CreateShareGroupCommand, ShareGroupDto>
{
    private readonly IDocumentSession _session;

    public CreateShareGroupHandler(IDocumentSession session)
    {
        _session = session;
    }

    public async Task<ShareGroupDto> Handle(CreateShareGroupCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Group name is required", nameof(request.Name));
        }

        var group = new ShareGroup
        {
            Name = request.Name.Trim(),
            OwnerUserId = request.OwnerUserId,
            MemberUserIds = request.MemberUserIds?.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>()
        };

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
