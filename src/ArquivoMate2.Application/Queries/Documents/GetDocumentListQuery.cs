using System.Collections.Generic;
using ArquivoMate2.Shared.Models;
using MediatR;

namespace ArquivoMate2.Application.Queries.Documents;

/// <summary>
/// Query to retrieve a filtered and paginated list of documents for a specific user.
/// </summary>
/// <param name="UserId">Identifier of the current user.</param>
/// <param name="Request">Filter, sorting and paging information.</param>
public sealed record GetDocumentListQuery(string UserId, DocumentListRequestDto Request) : IRequest<DocumentListQueryResultDto>;

/// <summary>
/// Result DTO for <see cref="GetDocumentListQuery"/>.
/// </summary>
public sealed class DocumentListQueryResultDto
{
    public long TotalCount { get; init; }
    public long PageCount { get; init; }
    public bool HasNextPage { get; init; }
    public bool HasPreviousPage { get; init; }
    public bool IsFirstPage { get; init; }
    public bool IsLastPage { get; init; }
    public int CurrentPage { get; init; }
    public IList<DocumentListItemDto> Documents { get; init; } = new List<DocumentListItemDto>();
}
