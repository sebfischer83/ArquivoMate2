using System.Collections.Generic;
using ArquivoMate2.Shared.Models.Sharing;
using MediatR;

namespace ArquivoMate2.Application.Commands.Sharing;

public record UpdateShareGroupCommand(string GroupId, string OwnerUserId, string Name, IReadOnlyCollection<string> MemberUserIds) : IRequest<ShareGroupDto?>;
