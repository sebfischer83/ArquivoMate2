using ArquivoMate2.Shared.Models.Sharing;
using MediatR;

namespace ArquivoMate2.Application.Commands.Sharing;

public record CreateShareAutomationRuleCommand(string OwnerUserId, ShareTarget Target, ShareAutomationScope Scope) : IRequest<ShareAutomationRuleDto>;
