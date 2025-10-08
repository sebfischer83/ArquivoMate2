using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Queries.Documents;
using ArquivoMate2.Domain.Collections;
using ArquivoMate2.Domain.ReadModels;
using ArquivoMate2.Shared.Models;
using AutoMapper;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Documents;

public class GetDocumentListQueryHandler : IRequestHandler<GetDocumentListQuery, DocumentListQueryResultDto>
{
    private readonly IQuerySession _querySession;
    private readonly ISearchClient _searchClient;
    private readonly IMapper _mapper;

    public GetDocumentListQueryHandler(IQuerySession querySession, ISearchClient searchClient, IMapper mapper)
    {
        _querySession = querySession;
        _searchClient = searchClient;
        _mapper = mapper;
    }

    public async Task<DocumentListQueryResultDto> Handle(GetDocumentListQuery request, CancellationToken cancellationToken)
    {
        var requestDto = request.Request;
        requestDto.NormalizePaging();

        IReadOnlyList<Guid>? searchIds = null;
        long? searchTotal = null;

        if (!string.IsNullOrWhiteSpace(requestDto.Search))
        {
            var searchResult = await _searchClient.SearchDocumentIdsAsync(request.UserId, requestDto.Search!, requestDto.Page, requestDto.PageSize, cancellationToken);
            searchIds = searchResult.Ids;
            searchTotal = searchResult.Total;

            if (searchIds.Count == 0)
            {
                return EmptyResult(requestDto.Page);
            }
        }

        var sharedAccessibleIdsList = await LoadSharedAccessibleDocumentIdsAsync(request.UserId, cancellationToken);
        IEnumerable<Guid>? sharedAccessibleIds = sharedAccessibleIdsList.Count > 0 ? sharedAccessibleIdsList : null;

        var baseQuery = _querySession.Query<DocumentView>()
            .ApplyDocumentFilters(requestDto, request.UserId, sharedAccessibleIds);

        if (requestDto.CollectionIds is { Count: > 0 })
        {
            var collectionDocumentIds = await LoadCollectionDocumentIdsAsync(request.UserId, requestDto.CollectionIds, cancellationToken);
            if (collectionDocumentIds.Count == 0)
            {
                return EmptyResult(requestDto.Page);
            }

            baseQuery = baseQuery.Where(d => collectionDocumentIds.Contains(d.Id));
        }

        if (searchIds != null)
        {
            baseQuery = baseQuery.Where(d => searchIds.Contains(d.Id));
        }

        baseQuery = baseQuery.ApplySorting(requestDto);

        if (searchIds != null)
        {
            var documents = await baseQuery.ToListAsyncFallback(cancellationToken);
            if (documents.Count == 0)
            {
                return EmptyResult(requestDto.Page);
            }

            var ordered = OrderBySearchRanking(documents, searchIds);
            var mapped = _mapper.Map<IList<DocumentListItemDto>>(ordered);
            var total = searchTotal ?? mapped.Count;
            var pageCount = (int)Math.Ceiling(total / (double)requestDto.PageSize);

            return new DocumentListQueryResultDto
            {
                Documents = mapped,
                TotalCount = total,
                PageCount = pageCount,
                HasNextPage = requestDto.Page < pageCount,
                HasPreviousPage = requestDto.Page > 1,
                IsFirstPage = requestDto.Page == 1,
                IsLastPage = requestDto.Page >= pageCount,
                CurrentPage = requestDto.Page
            };
        }
        else
        {
            var totalCount = await baseQuery.LongCountAsyncFallback(cancellationToken);
            if (totalCount == 0)
            {
                return EmptyResult(requestDto.Page);
            }

            var pageSize = requestDto.PageSize;
            var skip = (requestDto.Page - 1) * pageSize;
            var pagedItems = await baseQuery.Skip(skip).Take(pageSize).ToListAsyncFallback(cancellationToken);
            var mapped = _mapper.Map<IList<DocumentListItemDto>>(pagedItems);
            var pageCount = (int)Math.Ceiling(totalCount / (double)pageSize);

            return new DocumentListQueryResultDto
            {
                Documents = mapped,
                TotalCount = totalCount,
                PageCount = pageCount,
                HasNextPage = requestDto.Page < pageCount,
                HasPreviousPage = requestDto.Page > 1,
                IsFirstPage = requestDto.Page == 1,
                IsLastPage = requestDto.Page >= pageCount,
                CurrentPage = requestDto.Page
            };
        }
    }

    private static DocumentListQueryResultDto EmptyResult(int currentPage)
        => new()
        {
            Documents = new List<DocumentListItemDto>(),
            TotalCount = 0,
            PageCount = 0,
            HasNextPage = false,
            HasPreviousPage = false,
            IsFirstPage = currentPage == 1,
            IsLastPage = true,
            CurrentPage = currentPage
        };

    private async Task<List<Guid>> LoadSharedAccessibleDocumentIdsAsync(string userId, CancellationToken cancellationToken)
    {
        var query = _querySession.Query<DocumentAccessView>()
            .Where(a => a.EffectiveUserIds.Contains(userId) && a.OwnerUserId != userId)
            .Select(a => a.Id);

        return await query.ToListAsyncFallback(cancellationToken);
    }

    private async Task<List<Guid>> LoadCollectionDocumentIdsAsync(string userId, IList<Guid> collectionIds, CancellationToken cancellationToken)
    {
        var distinctIds = collectionIds.Distinct().ToList();
        var query = _querySession.Query<DocumentCollectionMembership>()
            .Where(m => m.OwnerUserId == userId && distinctIds.Contains(m.CollectionId))
            .Select(m => m.DocumentId)
            .Distinct();

        return await query.ToListAsyncFallback(cancellationToken);
    }

    private static List<DocumentView> OrderBySearchRanking(IReadOnlyCollection<DocumentView> documents, IReadOnlyList<Guid> searchIds)
    {
        var ranking = new Dictionary<Guid, int>();
        for (var i = 0; i < searchIds.Count; i++)
        {
            ranking[searchIds[i]] = i;
        }

        return documents
            .Where(d => ranking.ContainsKey(d.Id))
            .OrderBy(d => ranking[d.Id])
            .ToList();
    }

}
