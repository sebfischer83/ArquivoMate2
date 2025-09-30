using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces;

public interface IDocumentAccessService
{
    Task<bool> HasAccessToDocumentAsync(Guid documentId, string userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Guid>> GetSharedDocumentIdsAsync(string userId, CancellationToken cancellationToken);
}
