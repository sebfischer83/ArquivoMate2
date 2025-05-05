using Marten;
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
}
