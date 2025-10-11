using ArquivoMate2.Shared.Models;
using MediatR;
using System;

namespace ArquivoMate2.Application.Queries.Documents
{
    public sealed record AskCatalogDocumentQuestionQuery(string UserId, DocumentQuestionRequestDto Request) : IRequest<DocumentAnswerDto?>;
}
