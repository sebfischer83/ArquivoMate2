using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Document;
using Meilisearch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.Search
{
    public class SearchClient : ISearchClient
    {
        private readonly MeilisearchClient _meilisearchClient;

        public SearchClient(MeilisearchClient meilisearchClient)
        {
            _meilisearchClient = meilisearchClient;
        }

        public async Task<bool> AddDocument(Document document)
        {
            var index = _meilisearchClient.Index("documents");
            var task = await index.AddDocumentsAsync([SearchDocument.FromDocument(document)], "id");
            var res = await _meilisearchClient.WaitForTaskAsync(task.TaskUid);

            return res.Status == TaskInfoStatus.Succeeded;
        }

        public async Task<Dictionary<string, int>> GetFacetsAsync(string userId, CancellationToken cancellationToken)
        {
            var index = _meilisearchClient.Index("documents");
            var searchResult = await index.SearchAsync<SearchDocument>("", new SearchQuery
            {
                Filter = $"userId = \"{userId}\"",
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
            var task = await index.UpdateDocumentsAsync([SearchDocument.FromDocument(document)], "id");
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

            var result = await index.SearchAsync<SearchDocument>(search, new SearchQuery
            {
                Filter = $"userId = \"{userId}\"",
                Limit = pageSize,
                Offset = from
            }, cancellationToken);

            var ids = result.Hits?.Select(h => h.Id).ToList() ?? new List<Guid>();

            long total = ids.Count;
            // Reflection fallback auf EstimatedTotalHits falls vorhanden
            var estimatedProp = result.GetType().GetProperty("EstimatedTotalHits");
            var value = estimatedProp?.GetValue(result);
            if (value is int i) total = i;
            else if (value is long l) total = l;

            return (ids, total);
        }
    }
}
