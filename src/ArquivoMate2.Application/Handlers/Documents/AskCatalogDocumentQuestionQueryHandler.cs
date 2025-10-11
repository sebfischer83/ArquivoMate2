using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;
using ArquivoMate2.Application.Queries.Documents;
using ArquivoMate2.Application.Services.Documents;
using ArquivoMate2.Domain.ReadModels;
using ArquivoMate2.Shared.Models;
using Marten;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Handlers.Documents
{
    public class AskCatalogDocumentQuestionQueryHandler : IRequestHandler<AskCatalogDocumentQuestionQuery, DocumentAnswerDto?>
    {
        private readonly IQuerySession _querySession;
        private readonly IChatBot _chatBot;
        private readonly ISearchClient _searchClient;
        private readonly IFileMetadataService _fileMetadataService;
        private readonly ILogger<AskCatalogDocumentQuestionQueryHandler> _logger;

        public AskCatalogDocumentQuestionQueryHandler(
            IQuerySession querySession,
            IChatBot chatBot,
            ISearchClient searchClient,
            IFileMetadataService fileMetadataService,
            ILogger<AskCatalogDocumentQuestionQueryHandler> logger)
        {
            _querySession = querySession;
            _chatBot = chatBot;
            _searchClient = searchClient;
            _fileMetadataService = fileMetadataService;
            _logger = logger;
        }

        public async Task<DocumentAnswerDto?> Handle(AskCatalogDocumentQuestionQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Request.Question))
            {
                throw new ArgumentException("Question must not be empty.", nameof(request));
            }

            var totalDocuments = await _querySession.Query<DocumentView>()
                .Where(d => d.UserId == request.UserId && !d.Deleted)
                .CountAsync(cancellationToken);

            var context = new DocumentQuestionContext
            {
                DocumentId = Guid.Empty,
                Title = "Dokumentenkatalog",
                Summary = totalDocuments > 0
                    ? $"Der Benutzer besitzt aktuell {totalDocuments} Dokumente. Verwende query_documents, um spezifische Daten abzurufen."
                    : "Der Benutzer hat derzeit keine Dokumente im Katalog.",
                Keywords = Array.Empty<string>(),
                Content = string.Empty,
                Language = null,
                History = Array.Empty<string>()
            };

            var tooling = new DocumentQuestionTooling(request.UserId, null, _querySession, _searchClient, _fileMetadataService, _logger);

            var answer = await _chatBot.AnswerQuestion(context, request.Request.Question, tooling, cancellationToken);

            return new DocumentAnswerDto
            {
                Answer = answer.Answer,
                Model = string.IsNullOrWhiteSpace(answer.Model) ? _chatBot.ModelName : answer.Model,
                Citations = answer.Citations.Select(c => new DocumentAnswerCitationDto
                {
                    Source = c.Source,
                    Snippet = c.Snippet
                }).ToList(),
                Documents = answer.Documents.Select(d => new DocumentAnswerReferenceDto
                {
                    DocumentId = d.DocumentId,
                    Title = d.Title,
                    Summary = d.Summary,
                    Date = d.Date,
                    Score = d.Score,
                    FileSizeBytes = d.FileSizeBytes
                }).ToList(),
                DocumentCount = answer.DocumentCount
            };
        }
    }
}
