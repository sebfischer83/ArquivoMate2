using Marten;
using System;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Features.Processors.LabResults.Domain;
using ArquivoMate2.Application.Features.Processors.LabResults.Models;
using ArquivoMate2.Application.Interfaces;

namespace ArquivoMate2.Application.Features.Processors.LabResults.Services
{
    public interface ILabPivotUpdater
    {
        Task AddOrUpdateAsync(IDocumentSession session, LabResult report, IParameterNormalizer normalizer, CancellationToken cancellationToken = default);
        Task RebuildForOwnerAsync(IDocumentStore store, string ownerId, IParameterNormalizer normalizer, CancellationToken cancellationToken = default);
    }
}
