using System;
using ArquivoMate2.Shared.Models;
using MediatR;

namespace ArquivoMate2.Application.Queries.Documents;

/// <summary>
/// Query used to obtain an answer for a user question about a specific document.
/// </summary>
/// <param name="UserId">Identifier of the requesting user.</param>
/// <param name="DocumentId">Identifier of the document that should be queried.</param>
/// <param name="Request">The payload received from the API layer.</param>
public sealed record AskDocumentQuestionQuery(string UserId, Guid DocumentId, DocumentQuestionRequestDto Request) : IRequest<DocumentAnswerDto?>;
