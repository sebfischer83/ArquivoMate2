using ArquivoMate2.Domain.ReadModels;
using Marten;
using Meilisearch;
using Microsoft.Extensions.DependencyInjection;
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

            using var daemon = await _store.BuildProjectionDaemonAsync();
            await daemon.RebuildProjectionAsync<DocumentView>(CancellationToken.None);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class MeiliInitService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;

        public MeiliInitService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var client = scope.ServiceProvider.GetRequiredService<MeilisearchClient>();
                var stat = await client.CreateIndexAsync("documents", "id");
                await client.WaitForTaskAsync(stat.TaskUid, TimeSpan.FromMinutes(5).TotalMilliseconds);

                var index = await client.GetIndexAsync("documents");
                var updateTask = await index.UpdateSettingsAsync(
                    new Meilisearch.Settings()
                    {
                        FilterableAttributes = new List<string>() { "keywords", "userId", "allowedUserIds" },
                        SearchableAttributes = new List<string>() { "content", "summary", "title" },
                    });
                await client.WaitForTaskAsync(updateTask.TaskUid);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
