using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Shared.Models;
using Marten;

namespace ArquivoMate2.Application.Interfaces
{
    public interface IDocumentTypeEnricher
    {
        /// <summary>
        /// Enrich the provided DocumentDto with DocumentType metadata (SystemFeatures and UserFunctions) when available.
        /// </summary>
        Task EnrichAsync(DocumentDto dto, string? documentTypeName, IQuerySession querySession, CancellationToken cancellationToken = default);
    }
}
