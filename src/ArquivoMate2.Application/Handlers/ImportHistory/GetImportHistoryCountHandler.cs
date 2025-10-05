using ArquivoMate2.Application.Interfaces.ImportHistory;
using ArquivoMate2.Application.Queries.ImportHistory;
using MediatR;

namespace ArquivoMate2.Application.Handlers.ImportHistory
{
    public class GetImportHistoryCountHandler : IRequestHandler<GetImportHistoryCountQuery, int>
    {
        private readonly IImportHistoryReadStore _store;
        public GetImportHistoryCountHandler(IImportHistoryReadStore store) => _store = store;

        public Task<int> Handle(GetImportHistoryCountQuery request, CancellationToken cancellationToken)
            => _store.GetCountAsync(request.UserId, request.Status, cancellationToken);
    }
}
