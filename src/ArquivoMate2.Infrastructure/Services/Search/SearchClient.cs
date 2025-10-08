using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.ReadModels;
using Meilisearch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Marten;

namespace ArquivoMate2.Infrastructure.Services.Search
{
    public class SearchClient : ISearchClient
    {
        private readonly MeilisearchClient _meilisearchClient;
        private readonly IQuerySession _querySession;

        public SearchClient(MeilisearchClient meilisearchClient, IQuerySession querySession)
        {
            _meilisearchClient = meilisearchClient;
            _querySession = querySession;
        }

        private async Task<IReadOnlyCollection<string>> LoadAllowedUserIdsAsync(Guid documentId, string ownerUserId, CancellationToken ct)
        {
            var access = await _querySession.LoadAsync<DocumentAccessView>(documentId, ct);
            if (access == null) return Array.Empty<string>();
            if (access.EffectiveUserIds.Count == 0) return Array.Empty<string>();
            var allowed = access.EffectiveUserIds.Where(u => !string.Equals(u, ownerUserId, StringComparison.Ordinal)).Distinct().ToArray();
            return allowed;
        }

        public async Task<bool> AddDocument(Document document)
        {
            var index = _meilisearchClient.Index("documents");
            var allowed = await LoadAllowedUserIdsAsync(document.Id, document.UserId, CancellationToken.None);
            var task = await index.AddDocumentsAsync(new[] { SearchDocument.FromDocument(document, allowed) }, "id");
            var res = await _meilisearchClient.WaitForTaskAsync(task.TaskUid);
            return res.Status == TaskInfoStatus.Succeeded;
        }

        public async Task<Dictionary<string, int>> GetFacetsAsync(string userId, CancellationToken cancellationToken)
        {
            var index = _meilisearchClient.Index("documents");
            var filter = $"(userId = \"{userId}\" OR allowedUserIds = \"{userId}\")";
            var searchResult = await index.SearchAsync<SearchDocument>(string.Empty, new SearchQuery
            {
                Filter = filter,
                Facets = new List<string> { "keywords" },
                Limit = 0
            }, cancellationToken);

            if (searchResult.FacetDistribution != null && searchResult.FacetDistribution.TryGetValue("keywords", out var keywordsFacet))
            {
                return keywordsFacet.ToDictionary(f => f.Key, f => f.Value);
            }
            return new Dictionary<string, int>();
        }

        public async Task<bool> UpdateDocument(Document document)
        {
            var index = _meilisearchClient.Index("documents");
            var allowed = await LoadAllowedUserIdsAsync(document.Id, document.UserId, CancellationToken.None);
            var task = await index.UpdateDocumentsAsync(new[] { SearchDocument.FromDocument(document, allowed) }, "id");
            var res = await _meilisearchClient.WaitForTaskAsync(task.TaskUid);
            return res.Status == TaskInfoStatus.Succeeded;
        }

        public async Task<(IReadOnlyList<Guid> Ids, long Total)> SearchDocumentIdsAsync(string userId, string search, int page, int pageSize, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return (Array.Empty<Guid>(), 0);
            }

            var index = _meilisearchClient.Index("documents");
            var from = (page - 1) * pageSize;
            if (from < 0) from = 0;
            var filter = $"(userId = \"{userId}\" OR allowedUserIds = \"{userId}\")";

            var result = await index.SearchAsync<SearchDocument>(search, new SearchQuery
            {
                Filter = filter,
                Limit = pageSize,
                Offset = from
            }, cancellationToken);

            var ids = result.Hits?.Select(h => h.Id).ToList() ?? new List<Guid>();

            long total = ids.Count;
            var estimatedProp = result.GetType().GetProperty("EstimatedTotalHits");
            var value = estimatedProp?.GetValue(result);
            if (value is int i) total = i;
            else if (value is long l) total = l;

            return (ids, total);
        }

        public async Task UpdateDocumentAccessAsync(Guid documentId, IReadOnlyCollection<string> allowedUserIds, CancellationToken cancellationToken)
        {
            var index = _meilisearchClient.Index("documents");
            var payload = new[] { new { id = documentId, allowedUserIds = allowedUserIds } };
            var task = await index.UpdateDocumentsAsync(payload, "id");
            await _meilisearchClient.WaitForTaskAsync(task.TaskUid);
        }
    }
}
