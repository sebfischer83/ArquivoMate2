using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;
using ArquivoMate2.Domain.ReadModels;
using Marten;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Services.Documents
{
    /// <summary>
    /// Implements the document querying tooling exposed to the chatbot via function calls.
    /// </summary>
    public sealed class DocumentQuestionTooling : IDocumentQuestionTooling
    {
        private const int MaxLimit = 20;
        private const int SearchBufferMultiplier = 3;
        private const int MaxSearchCandidates = 60;
        private const double BytesPerMegabyte = 1024d * 1024d;

        private readonly string _userId;
        private readonly Guid? _excludedDocumentId;
        private readonly IQuerySession _querySession;
        private readonly ISearchClient _searchClient;
        private readonly IFileMetadataService _fileMetadataService;
        private readonly ILogger _logger;

        public DocumentQuestionTooling(
            string userId,
            Guid? excludedDocumentId,
            IQuerySession querySession,
            ISearchClient searchClient,
            IFileMetadataService fileMetadataService,
            ILogger logger)
        {
            _userId = userId;
            _excludedDocumentId = excludedDocumentId == Guid.Empty ? null : excludedDocumentId;
            _querySession = querySession;
            _searchClient = searchClient;
            _fileMetadataService = fileMetadataService;
            _logger = logger;
        }

        public async Task<DocumentQueryResult> QueryDocumentsAsync(DocumentQuery query, CancellationToken cancellationToken)
        {
            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            var limit = query.Limit;
            if (limit <= 0) limit = 5;
            if (limit > MaxLimit) limit = MaxLimit;

            var filters = query.Filters ?? new DocumentQueryFilters();
            var requestedIds = filters.DocumentIds ?? Array.Empty<Guid>();

            var baseQuery = _querySession.Query<DocumentView>()
                .Where(d => d.UserId == _userId && !d.Deleted);

            if (_excludedDocumentId.HasValue)
            {
                baseQuery = baseQuery.Where(d => d.Id != _excludedDocumentId.Value);
            }

            if (filters.Year.HasValue)
            {
                baseQuery = baseQuery.Where(d => d.Date.HasValue && d.Date.Value.Year == filters.Year.Value);
            }

            if (!string.IsNullOrWhiteSpace(filters.Type))
            {
                baseQuery = baseQuery.Where(d => d.Type == filters.Type);
            }

            if (requestedIds.Count > 0)
            {
                baseQuery = baseQuery.Where(d => requestedIds.Contains(d.Id));
            }

            var requiresSizeFilter = filters.MinFileSizeMb.HasValue || filters.MaxFileSizeMb.HasValue;
            var needsCount = query.Projection is DocumentQueryProjection.Count or DocumentQueryProjection.Both;

            IReadOnlyList<DocumentView> candidates;
            long? totalCount = null;

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var searchLimit = Math.Min(MaxSearchCandidates, Math.Max(limit * SearchBufferMultiplier, limit));
                var search = await _searchClient.SearchDocumentIdsAsync(_userId, query.Search!, 1, searchLimit, cancellationToken);
                var searchIds = search.Ids ?? Array.Empty<Guid>();
                totalCount = search.Total;

                if (searchIds.Count == 0)
                {
                    return new DocumentQueryResult
                    {
                        Documents = Array.Empty<DocumentQueryDocument>(),
                        TotalCount = requiresSizeFilter ? 0 : totalCount
                    };
                }

                baseQuery = baseQuery.Where(d => searchIds.Contains(d.Id));
                candidates = await baseQuery.ToListAsync(cancellationToken);

                candidates = searchIds
                    .Select(id => candidates.FirstOrDefault(d => d.Id == id))
                    .Where(d => d != null)
                    .Cast<DocumentView>()
                    .ToList();
            }
            else if (requiresSizeFilter)
            {
                // Load the full candidate set so size filters and counts are accurate.
                candidates = await baseQuery.ToListAsync(cancellationToken);
            }
            else
            {
                if (needsCount)
                {
                    totalCount = await baseQuery.CountAsync(cancellationToken);
                }

                var fetchLimit = Math.Max(limit * SearchBufferMultiplier, limit);
                candidates = await baseQuery
                    .OrderByDescending(d => d.ProcessedAt ?? d.UploadedAt)
                    .Take(fetchLimit)
                    .ToListAsync(cancellationToken);
            }

            var filtered = new List<(DocumentView View, long? SizeBytes)>();

            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                long? sizeBytes = null;
                if (requiresSizeFilter)
                {
                    try
                    {
                        var metadata = await _fileMetadataService.ReadMetadataAsync(candidate.Id, candidate.UserId, cancellationToken);
                        sizeBytes = metadata?.Size;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to read metadata for document {DocumentId}", candidate.Id);
                        continue;
                    }

                    if (!MatchesSize(sizeBytes, filters.MinFileSizeMb, filters.MaxFileSizeMb))
                    {
                        continue;
                    }
                }

                filtered.Add((candidate, sizeBytes));
            }

            if (requiresSizeFilter && needsCount)
            {
                totalCount = filtered.Count;
            }
            else if (needsCount && totalCount is null)
            {
                totalCount = filtered.Count;
            }

            if (requestedIds.Count > 0)
            {
                filtered = filtered
                    .Where(tuple => requestedIds.Contains(tuple.View.Id))
                    .ToList();
            }

            var hits = filtered
                .Take(limit)
                .Select(tuple => new DocumentQueryDocument
                {
                    DocumentId = tuple.View.Id,
                    Title = tuple.View.Title,
                    Summary = tuple.View.Summary,
                    Date = tuple.View.Date,
                    FileSizeBytes = tuple.SizeBytes,
                    Score = null
                })
                .ToList();

            return new DocumentQueryResult
            {
                Documents = hits,
                TotalCount = totalCount
            };
        }

        private static bool MatchesSize(long? sizeBytes, double? minMb, double? maxMb)
        {
            if (!minMb.HasValue && !maxMb.HasValue)
            {
                return true;
            }

            if (!sizeBytes.HasValue)
            {
                return false;
            }

            if (minMb.HasValue && sizeBytes.Value < minMb.Value * BytesPerMegabyte)
            {
                return false;
            }

            if (maxMb.HasValue && sizeBytes.Value > maxMb.Value * BytesPerMegabyte)
            {
                return false;
            }

            return true;
        }
    }
}
