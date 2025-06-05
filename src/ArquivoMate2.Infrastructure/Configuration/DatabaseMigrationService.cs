using Marten;
using Meilisearch;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Configuration
{
    public class DatabaseMigrationService : IHostedService
    {
        private readonly IDocumentStore _store;

        public DatabaseMigrationService(IDocumentStore store)
        {
            _store = store;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class MeiliInitService : IHostedService
    {
        private readonly MeilisearchClient _client;

        public MeiliInitService(MeilisearchClient client)
        {
            _client = client;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var stat = await _client.CreateIndexAsync("documents", "id");
            await _client.WaitForTaskAsync(stat.TaskUid, TimeSpan.FromMinutes(5).TotalMilliseconds);

            var index = await _client.GetIndexAsync("documents");
            var updateTask = await index.UpdateSettingsAsync(
                new Meilisearch.Settings()
                {
                    FilterableAttributes = new List<string>() { "keywords", "userid" },
                    SearchableAttributes = new List<string>() { "content", "summary", "title" },
                });
            await _client.WaitForTaskAsync(updateTask.TaskUid);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
