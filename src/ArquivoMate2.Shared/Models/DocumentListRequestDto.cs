using System;
using System.Collections.Generic;

namespace ArquivoMate2.Shared.Models
{
    public class DocumentListRequestDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        // Filter fields
        public string? Type { get; set; }
        public bool? Accepted { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public decimal? MinTotalPrice { get; set; }
        public decimal? MaxTotalPrice { get; set; }
        public string? CustomerNumber { get; set; }
        public string? InvoiceNumber { get; set; }
        public List<string>? Keywords { get; set; }
        public bool KeywordMatchAll { get; set; } = false;

        // Full-text search (via Meilisearch)
        public string? Search { get; set; }

        // Sorting options
        public string? SortBy { get; set; }
        public string? SortDirection { get; set; }
    }
}
