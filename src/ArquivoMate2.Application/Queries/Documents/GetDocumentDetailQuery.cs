using System;
using ArquivoMate2.Shared.Models;
using MediatR;

namespace ArquivoMate2.Application.Queries.Documents;

/// <summary>
/// Query for retrieving a single document including its history.
/// </summary>
/// <param name="UserId">Identifier of the current user.</param>
/// <param name="DocumentId">Document identifier.</param>
public sealed record GetDocumentDetailQuery(string UserId, Guid DocumentId) : IRequest<DocumentDetailQueryResultDto?>;

/// <summary>
/// Result DTO for <see cref="GetDocumentDetailQuery"/>.
/// </summary>
public sealed class DocumentDetailQueryResultDto
{
    public DocumentDto Document { get; init; } = new();
}
