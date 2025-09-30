using System;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Domain.Sharing;

namespace ArquivoMate2.Application.Interfaces;

public interface IAutoShareService
{
    Task ApplyRulesAsync(Guid documentId, string ownerUserId, CancellationToken cancellationToken);

    Task ApplyRuleToExistingDocumentsAsync(ShareAutomationRule rule, CancellationToken cancellationToken);
}
