using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Shared.Models.Grouping;

namespace ArquivoMate2.Application.Interfaces.Grouping;

public interface IDocumentGroupingService
{
    Task<IReadOnlyList<DocumentGroupingNode>> GroupAsync(string ownerUserId, DocumentGroupingRequest request, CancellationToken ct);
}
