using ArquivoMate2.Application.Interfaces.ImportHistory;
using ArquivoMate2.Domain.ReadModels;
using ArquivoMate2.Infrastructure.Persistance;
using ArquivoMate2.Shared.Models;
using Marten;
using Marten.Pagination;

namespace ArquivoMate2.Infrastructure.Services.ImportHistory
{
    public class ImportHistoryReadStore : IImportHistoryReadStore
    {
        private readonly IQuerySession _query;
        private readonly AutoMapper.IMapper _mapper;

        public ImportHistoryReadStore(IQuerySession query, AutoMapper.IMapper mapper)
        {
            _query = query;
            _mapper = mapper;
        }

        public async Task<ImportHistoryListDto> GetListAsync(string userId, int page, int pageSize, DocumentProcessingStatus? status, CancellationToken ct)
        {
            var q = _query.Query<ImportHistoryView>().Where(x => x.UserId == userId && !x.IsHidden);
            if (status.HasValue)
            {
                q = q.Where(x => x.Status == status.Value);
            }

            var paged = await q.ToPagedListAsync(page, pageSize, ct);

            if (paged.Count == 0)
            {
                return new ImportHistoryListDto
                {
                    Items = Array.Empty<ImportHistoryListItemDto>(),
                    TotalCount = 0,
                    PageCount = 0,
                    HasNextPage = false,
                    HasPreviousPage = false,
                    IsLastPage = true,
                    IsFirstPage = true,
                    CurrentPage = page
                };
            }

            var items = _mapper.Map<ImportHistoryListItemDto[]>(paged);
            return new ImportHistoryListDto
            {
                Items = items,
                TotalCount = paged.TotalItemCount,
                PageCount = paged.PageCount,
                HasNextPage = paged.HasNextPage,
                HasPreviousPage = paged.HasPreviousPage,
                IsFirstPage = paged.IsFirstPage,
                IsLastPage = paged.IsLastPage,
                CurrentPage = page
            };
        }

        public Task<int> GetCountAsync(string userId, DocumentProcessingStatus status, CancellationToken ct)
        {
            return _query.Query<ImportHistoryView>()
                .Where(x => x.UserId == userId && x.Status == status && !x.IsHidden)
                .CountAsync(ct);
        }
    }
}
