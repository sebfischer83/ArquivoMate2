using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Shared.Models.Sharing;

namespace ArquivoMate2.Application.Interfaces;

public interface IDocumentAccessService
{
    Task<bool> HasAccessToDocumentAsync(Guid documentId, string userId, CancellationToken cancellationToken);

    Task<bool> HasEditAccessToDocumentAsync(Guid documentId, string userId, CancellationToken cancellationToken);

    Task<bool> HasPermissionAsync(Guid documentId, string userId, DocumentPermissions permission, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Guid>> GetSharedDocumentIdsAsync(string userId, CancellationToken cancellationToken);
}
