using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces.Sharing;

/// <summary>
/// Abstraction to look up minimal ownership information for a document without leaking infrastructure read models.
/// </summary>
public interface IDocumentOwnershipLookup
{
    /// <summary>
    /// Returns ownership information for a document or null if it does not exist.
    /// Deleted documents may still be returned with Deleted = true (for authorization decisions).
    /// </summary>
    Task<DocumentOwnerInfo?> GetAsync(Guid documentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Minimal ownership info used by Application layer.
/// </summary>
/// <param name="Id">Document Id</param>
/// <param name="UserId">Owner user id</param>
/// <param name="Deleted">Indicates soft delete flag</param>
public readonly record struct DocumentOwnerInfo(Guid Id, string UserId, bool Deleted);
