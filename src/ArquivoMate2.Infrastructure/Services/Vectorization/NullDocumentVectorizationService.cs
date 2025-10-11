using ArquivoMate2.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.Vectorization
{
    public sealed class NullDocumentVectorizationService : IDocumentVectorizationService
    {
        private readonly ILogger<NullDocumentVectorizationService> _logger;

        public NullDocumentVectorizationService(ILogger<NullDocumentVectorizationService> logger)
        {
            _logger = logger;
            _logger.LogInformation("Vector store connection string not configured. Document questions will fall back to keyword search only.");
        }

        public Task StoreDocumentAsync(Guid documentId, string userId, string content, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task DeleteDocumentAsync(Guid documentId, string userId, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<IReadOnlyList<string>> FindRelevantChunkIdsAsync(Guid documentId, string userId, string question, int limit, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
}
