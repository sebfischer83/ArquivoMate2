using MediatR;

namespace ArquivoMate2.Application.Commands.Sharing;

public record DeleteShareAutomationRuleCommand(string RuleId, string OwnerUserId) : IRequest<bool>;
