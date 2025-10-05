using ArquivoMate2.Shared.Models;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces.ImportHistory
{
    /// <summary>
    /// Abstraction for reading import history projections.
    /// </summary>
    public interface IImportHistoryReadStore
    {
        Task<ImportHistoryListDto> GetListAsync(string userId, int page, int pageSize, ArquivoMate2.Shared.Models.DocumentProcessingStatus? status, CancellationToken ct);
        Task<int> GetCountAsync(string userId, ArquivoMate2.Shared.Models.DocumentProcessingStatus status, CancellationToken ct);
    }
}
