using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Features.Processors.LabResults.Domain;
using ArquivoMate2.Application.Features.Processors.LabResults.Models;
using ArquivoMate2.Application.Features.Processors.LabResults.Services;
using ArquivoMate2.Application.Interfaces;
using Marten;

namespace ArquivoMate2.Application.Features.Processors.LabResults.Services
{
    public class NoopLabPivotUpdater : ILabPivotUpdater
    {
        public Task AddOrUpdateAsync(IDocumentSession session, LabResult report, IParameterNormalizer normalizer, CancellationToken cancellationToken = default)
        {
            // No-op for unit tests that don't need pivot updates
            return Task.CompletedTask;
        }

        public Task RebuildForOwnerAsync(IDocumentStore store, string ownerId, IParameterNormalizer normalizer, CancellationToken cancellationToken = default)
        {
            // No-op
            return Task.CompletedTask;
        }
    }
}
