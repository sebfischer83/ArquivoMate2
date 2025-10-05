using ArquivoMate2.Shared.Models;
using MediatR;

namespace ArquivoMate2.Application.Queries.ImportHistory
{
    public sealed record GetImportHistoryListQuery(int Page, int PageSize, string UserId, DocumentProcessingStatus? Status) : IRequest<ImportHistoryListDto>;
}
