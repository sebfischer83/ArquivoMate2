using ArquivoMate2.Application.Interfaces.ImportHistory;
using ArquivoMate2.Application.Queries.ImportHistory;
using ArquivoMate2.Shared.Models;
using MediatR;

namespace ArquivoMate2.Application.Handlers.ImportHistory
{
    public class GetImportHistoryListHandler : IRequestHandler<GetImportHistoryListQuery, ImportHistoryListDto>
    {
        private readonly IImportHistoryReadStore _store;
        public GetImportHistoryListHandler(IImportHistoryReadStore store) => _store = store;

        public Task<ImportHistoryListDto> Handle(GetImportHistoryListQuery request, CancellationToken cancellationToken)
        {
            var page = request.Page < 1 ? 1 : request.Page;
            var size = request.PageSize <= 0 ? 10 : (request.PageSize > 200 ? 200 : request.PageSize);
            return _store.GetListAsync(request.UserId, page, size, request.Status, cancellationToken);
        }
    }
}
