using ArquivoMate2.Domain.Document;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    public interface ISearchClient
    {
        Task<bool> AddDocument(Document document);
        Task<bool> UpdateDocument(Document document);
        Task<Dictionary<string, int>> GetFacetsAsync(string userId, CancellationToken cancellationToken);
        Task<(IReadOnlyList<Guid> Ids, long Total)> SearchDocumentIdsAsync(string userId, string search, int page, int pageSize, CancellationToken cancellationToken);

        // NEW: partial access update
        Task UpdateDocumentAccessAsync(Guid documentId, IReadOnlyCollection<string> allowedUserIds, CancellationToken cancellationToken);
    }
}
