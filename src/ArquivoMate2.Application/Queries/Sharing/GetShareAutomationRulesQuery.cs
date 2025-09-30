using System.Collections.Generic;
using ArquivoMate2.Shared.Models.Sharing;
using MediatR;

namespace ArquivoMate2.Application.Queries.Sharing;

public record GetShareAutomationRulesQuery(string OwnerUserId) : IRequest<IReadOnlyCollection<ShareAutomationRuleDto>>;
