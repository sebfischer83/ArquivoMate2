using ArquivoMate2.Shared.Models;
using MediatR;

namespace ArquivoMate2.Application.Queries.ImportHistory
{
    public sealed record GetImportHistoryCountQuery(string UserId, DocumentProcessingStatus Status) : IRequest<int>;
}
