using ArquivoMate2.Application.Interfaces;
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
    }
}
