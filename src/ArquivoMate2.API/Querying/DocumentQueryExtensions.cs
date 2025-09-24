using System;
using System.Linq;
using ArquivoMate2.Infrastructure.Persistance;
using ArquivoMate2.Shared.Models;

namespace ArquivoMate2.API.Querying
{
    public static class DocumentQueryExtensions
    {
        public static IQueryable<DocumentView> ApplyDocumentFilters(this IQueryable<DocumentView> query, DocumentListRequestDto dto, string userId)
        {
            query = query.Where(d => d.UserId == userId && d.Processed && !d.Deleted);

            if (!string.IsNullOrWhiteSpace(dto.Type))
                query = query.Where(d => d.Type == dto.Type);
            if (dto.Accepted.HasValue)
                query = query.Where(d => d.Accepted == dto.Accepted.Value);
            if (dto.FromDate.HasValue)
                query = query.Where(d => d.Date != null && d.Date >= dto.FromDate.Value);
            if (dto.ToDate.HasValue)
                query = query.Where(d => d.Date != null && d.Date <= dto.ToDate.Value);
            if (dto.MinTotalPrice.HasValue)
                query = query.Where(d => d.TotalPrice != null && d.TotalPrice >= dto.MinTotalPrice.Value);
            if (dto.MaxTotalPrice.HasValue)
                query = query.Where(d => d.TotalPrice != null && d.TotalPrice <= dto.MaxTotalPrice.Value);
            if (!string.IsNullOrWhiteSpace(dto.CustomerNumber))
                query = query.Where(d => d.CustomerNumber == dto.CustomerNumber);
            if (!string.IsNullOrWhiteSpace(dto.InvoiceNumber))
                query = query.Where(d => d.InvoiceNumber == dto.InvoiceNumber);

            if (dto.Keywords is { Count: > 0 })
            {
                foreach (var kw in dto.Keywords.Where(k => !string.IsNullOrWhiteSpace(k)))
                {
                    var kLocal = kw.Trim();
                    query = query.Where(d => d.Keywords.Contains(kLocal));
                }
            }
            return query;
        }

        public static IQueryable<DocumentView> ApplySorting(this IQueryable<DocumentView> query, DocumentListRequestDto dto)
        {
            var sortBy = dto.SortBy?.Trim().ToLowerInvariant();
            var direction = dto.SortDirection?.Trim().ToLowerInvariant() == "asc" ? "asc" : "desc";

            return sortBy switch
            {
                "title" => direction == "asc" ? query.OrderBy(d => d.Title).ThenBy(d => d.Id) : query.OrderByDescending(d => d.Title).ThenByDescending(d => d.Id),
                "totalprice" => direction == "asc" ? query.OrderBy(d => d.TotalPrice).ThenBy(d => d.Id) : query.OrderByDescending(d => d.TotalPrice).ThenByDescending(d => d.Id),
                "occurredon" => direction == "asc" ? query.OrderBy(d => d.OccurredOn).ThenBy(d => d.Id) : query.OrderByDescending(d => d.OccurredOn).ThenByDescending(d => d.Id),
                "date" => direction == "asc" ? query.OrderBy(d => d.Date).ThenBy(d => d.Id) : query.OrderByDescending(d => d.Date).ThenByDescending(d => d.Id),
                "type" => direction == "asc" ? query.OrderBy(d => d.Type).ThenBy(d => d.Id) : query.OrderByDescending(d => d.Type).ThenByDescending(d => d.Id),
                "accepted" => direction == "asc" ? query.OrderBy(d => d.Accepted).ThenBy(d => d.Id) : query.OrderByDescending(d => d.Accepted).ThenByDescending(d => d.Id),
                _ => direction == "asc"
                    // Fallback: erst nach Date (NULLs zuerst), danach OccurredOn, dann Id
                    ? query.OrderBy(d => d.Date).ThenBy(d => d.OccurredOn).ThenBy(d => d.Id)
                    // Desc: neueste Date zuerst, bei NULL Date nach OccurredOn
                    : query.OrderByDescending(d => d.Date).ThenByDescending(d => d.OccurredOn).ThenByDescending(d => d.Id)
            };
        }

        public static void NormalizePaging(this DocumentListRequestDto dto, int maxPageSize = 100)
        {
            if (dto.Page < 1) dto.Page = 1;
            if (dto.PageSize < 1) dto.PageSize = 1;
            if (dto.PageSize > maxPageSize) dto.PageSize = maxPageSize;
        }
    }
}
