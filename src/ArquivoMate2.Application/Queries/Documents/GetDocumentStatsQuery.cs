using System.Collections.Generic;
using ArquivoMate2.Shared.Models;
using MediatR;

namespace ArquivoMate2.Application.Queries.Documents;

/// <summary>
/// Query for retrieving aggregated document statistics for the current user.
/// </summary>
/// <param name="UserId">Identifier of the current user.</param>
public sealed record GetDocumentStatsQuery(string UserId) : IRequest<DocumentStatsQueryResultDto>;

/// <summary>
/// Result DTO for <see cref="GetDocumentStatsQuery"/>.
/// </summary>
public sealed class DocumentStatsQueryResultDto
{
    public int Documents { get; init; }
    public int NotAccepted { get; init; }
    public int Characters { get; init; }
    public Dictionary<string, int> Facets { get; init; } = new();
}
