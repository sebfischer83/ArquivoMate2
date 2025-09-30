using System.Threading;
using System.Threading.Tasks;
using System;
using ArquivoMate2.Domain.Sharing;

namespace ArquivoMate2.Application.Interfaces.Sharing;

/// <summary>
/// Port for maintaining the denormalized document access view.
/// </summary>
public interface IDocumentAccessUpdater
{
    Task AddShareAsync(DocumentShare share, CancellationToken cancellationToken = default);
    Task RemoveShareAsync(DocumentShare share, CancellationToken cancellationToken = default);
}
