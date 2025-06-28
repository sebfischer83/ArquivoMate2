using System.Collections.Generic;

namespace ArquivoMate2.Shared.Models
{
    public class ImportHistoryListDto
    {
        public long TotalCount { get; set; }
        public long PageCount { get; set; }
        public bool IsLastPage { get; set; }
        public bool IsFirstPage { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
        public int CurrentPage { get; set; }
        public IList<ImportHistoryListItemDto> Items { get; set; } = new List<ImportHistoryListItemDto>();
    }
}
