using System;
using System.Collections.Generic;
using System.Linq;
using ArquivoMate2.Infrastructure.Persistance;
using ArquivoMate2.Shared.Models;

namespace ArquivoMate2.API.Querying
{
    public static class DocumentQueryExtensions
    {
        public static IQueryable<DocumentView> ApplyDocumentFilters(this IQueryable<DocumentView> query, DocumentListRequestDto dto, string userId, IEnumerable<Guid>? accessibleSharedDocumentIds)
        {
            // Only processed & not deleted, and either owned or in accessible set
            if (accessibleSharedDocumentIds != null)
            {
                var sharedIdsList = accessibleSharedDocumentIds as ICollection<Guid> ?? accessibleSharedDocumentIds.ToList();
                query = query.Where(d => d.Processed && !d.Deleted && (d.UserId == userId || sharedIdsList.Contains(d.Id)));
            }
            else
            {
                query = query.Where(d => d.Processed && !d.Deleted && d.UserId == userId);
            }

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

            // Year / Month derived from (Date ?? UploadedAt) => treat null Date as fallback UploadedAt
            if (dto.Year.HasValue)
            {
                int year = dto.Year.Value;
                query = query.Where(d => ((d.Date ?? d.UploadedAt).Year) == year);
            }
            if (dto.Month.HasValue)
            {
                int month = dto.Month.Value;
                if (month is < 1 or > 12)
                {
                    // impossible month will yield no results; simplify by short-circuiting
                    return query.Where(_ => false);
                }
                query = query.Where(d => ((d.Date ?? d.UploadedAt).Month) == month);
            }
            if (!string.IsNullOrWhiteSpace(dto.Language))
            {
                var lang = dto.Language.Trim();
                query = query.Where(d => d.Language == lang);
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
                    ? query.OrderBy(d => d.Date).ThenBy(d => d.OccurredOn).ThenBy(d => d.Id)
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
