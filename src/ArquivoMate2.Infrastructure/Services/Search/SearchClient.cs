﻿using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Document;
using Meilisearch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

            var result = new Dictionary<string, int>();
            if (searchResult.FacetDistribution != null && searchResult.FacetDistribution.TryGetValue("keywords", out var keywordsFacet))
            {
                return keywordsFacet.ToDictionary(f => f.Key, f => f.Value);
            }

            throw new InvalidOperationException("No facets found for the specified user.");
        }

        public async Task<List<string>> ListUserIdsAsync(CancellationToken cancellationToken)
        {
            var index = _meilisearchClient.Index("documents");
            var searchResult = await index.SearchAsync<SearchDocument>("", new SearchQuery
            {
                Limit = 1000, // adjust as needed for your dataset size
                AttributesToRetrieve = new List<string> { "userId" }
            }, cancellationToken);

            return searchResult.Hits
                .Select(doc => (doc as SearchDocument)?.UserId)
                .Where(uid => !string.IsNullOrEmpty(uid))
                .Distinct()
                .ToList();
        }
    }
}
