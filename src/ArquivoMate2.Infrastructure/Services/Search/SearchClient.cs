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
using System.Diagnostics;
using Polly;
using Polly.Contrib.WaitAndRetry;
using System.Net.Http;

namespace ArquivoMate2.Infrastructure.Services.Search
{
    public class SearchClient : ISearchClient
    {
        private readonly MeilisearchClient _meilisearchClient;
        private readonly IQuerySession _querySession;
        private static readonly ActivitySource s_activity = new("ArquivoMate2.SearchClient", "1.0");

        // Retry policy for Meilisearch operations
        private readonly AsyncPolicy _meiliRetryPolicy;

        public SearchClient(MeilisearchClient meilisearchClient, IQuerySession querySession)
        {
            _meilisearchClient = meilisearchClient;
            _querySession = querySession;

            // Initialize Polly retry policy: handle transient exceptions and retry with exponential backoff (5 attempts)
            _meiliRetryPolicy = Policy
                .Handle<Exception>()
                .Or<HttpRequestException>()
                .WaitAndRetryAsync(Backoff.ExponentialBackoff(TimeSpan.FromMilliseconds(200), 5));
        }

        private async Task<IReadOnlyCollection<string>> LoadAllowedUserIdsAsync(Guid documentId, string ownerUserId, CancellationToken ct)
        {
            using var a = s_activity.StartActivity("SearchClient.LoadAllowedUserIds", ActivityKind.Internal);
            var access = await _querySession.LoadAsync<DocumentAccessView>(documentId, ct);
            if (access == null)
            {
                a?.SetTag("access.found", false);
                return Array.Empty<string>();
            }
            a?.SetTag("access.found", true);
            if (access.EffectiveUserIds.Count == 0) return Array.Empty<string>();
            var allowed = access.EffectiveUserIds.Where(u => !string.Equals(u, ownerUserId, StringComparison.Ordinal)).Distinct().ToArray();
            a?.SetTag("access.allowedCount", allowed.Length);
            return allowed;
        }

        public async Task<bool> AddDocument(Document document)
        {
            using var a = s_activity.StartActivity("SearchClient.AddDocument", ActivityKind.Internal);
            var index = _meilisearchClient.Index("documents");
            var allowed = await LoadAllowedUserIdsAsync(document.Id, document.UserId, CancellationToken.None);
            a?.SetTag("document.id", document.Id);
            a?.SetTag("document.allowedCount", allowed.Count);

            // MINILI_RETRY: wrapped
            var succeeded = await _meiliRetryPolicy.ExecuteAsync(async () =>
            {
                var task = await index.AddDocumentsAsync(new[] { SearchDocument.FromDocument(document, allowed) }, "id");
                a?.SetTag("meili.taskUid", task.TaskUid);
                var res = await _meilisearchClient.WaitForTaskAsync(task.TaskUid);
                a?.SetTag("meili.status", res.Status.ToString());
                return res.Status == TaskInfoStatus.Succeeded;
            }).ConfigureAwait(false);

            return succeeded;
        }

        public async Task<Dictionary<string, int>> GetFacetsAsync(string userId, CancellationToken cancellationToken)
        {
            using var a = s_activity.StartActivity("SearchClient.GetFacets", ActivityKind.Internal);
            var index = _meilisearchClient.Index("documents");
            var filter = $"(userId = \"{userId}\" OR allowedUserIds = \"{userId}\")";
            a?.SetTag("facets.userId", userId);
            var searchResult = await index.SearchAsync<SearchDocument>(string.Empty, new SearchQuery
            {
                Filter = filter,
                Facets = new List<string> { "keywords" },
                Limit = 0
            }, cancellationToken);

            if (searchResult.FacetDistribution != null && searchResult.FacetDistribution.TryGetValue("keywords", out var keywordsFacet))
            {
                a?.SetTag("facets.count", keywordsFacet.Count);
                return keywordsFacet.ToDictionary(f => f.Key, f => f.Value);
            }
            a?.SetTag("facets.count", 0);
            return new Dictionary<string, int>();
        }

        public async Task<bool> UpdateDocument(Document document)
        {
            using var a = s_activity.StartActivity("SearchClient.UpdateDocument", ActivityKind.Internal);
            var index = _meilisearchClient.Index("documents");
            var allowed = await LoadAllowedUserIdsAsync(document.Id, document.UserId, CancellationToken.None);
            a?.SetTag("document.id", document.Id);
            a?.SetTag("document.allowedCount", allowed.Count);
            var task = await index.UpdateDocumentsAsync(new[] { SearchDocument.FromDocument(document, allowed) }, "id");
            a?.SetTag("meili.taskUid", task.TaskUid);
            var res = await _meilisearchClient.WaitForTaskAsync(task.TaskUid);
            a?.SetTag("meili.status", res.Status.ToString());
            return res.Status == TaskInfoStatus.Succeeded;
        }

        public async Task<(IReadOnlyList<Guid> Ids, long Total)> SearchDocumentIdsAsync(string userId, string search, int page, int pageSize, CancellationToken cancellationToken)
        {
            using var a = s_activity.StartActivity("SearchClient.SearchDocumentIds", ActivityKind.Internal);
            if (string.IsNullOrWhiteSpace(search))
            {
                a?.SetTag("search.empty", true);
                return (Array.Empty<Guid>(), 0);
            }

            var index = _meilisearchClient.Index("documents");
            var from = (page - 1) * pageSize;
            if (from < 0) from = 0;
            var filter = $"(userId = \"{userId}\" OR allowedUserIds = \"{userId}\")";
            a?.SetTag("search.query", search);
            a?.SetTag("search.page", page);
            a?.SetTag("search.pageSize", pageSize);

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

            a?.SetTag("search.found", ids.Count);
            a?.SetTag("search.estimatedTotal", total);
            return (ids, total);
        }

        public async Task UpdateDocumentAccessAsync(Guid documentId, IReadOnlyCollection<string> allowedUserIds, CancellationToken cancellationToken)
        {
            using var a = s_activity.StartActivity("SearchClient.UpdateDocumentAccess", ActivityKind.Internal);
            var index = _meilisearchClient.Index("documents");
            a?.SetTag("document.id", documentId);
            a?.SetTag("access.count", allowedUserIds?.Count ?? 0);
            var payload = new[] { new { id = documentId, allowedUserIds = allowedUserIds } };
            var task = await index.UpdateDocumentsAsync(payload, "id");
            a?.SetTag("meili.taskUid", task.TaskUid);
            await _meilisearchClient.WaitForTaskAsync(task.TaskUid);
            a?.SetTag("meili.updateCompleted", true);
        }
    }
}
