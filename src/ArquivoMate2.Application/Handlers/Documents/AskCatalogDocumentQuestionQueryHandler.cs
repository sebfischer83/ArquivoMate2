using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;
using ArquivoMate2.Application.Queries.Documents;
using ArquivoMate2.Application.Services.Documents;
using ArquivoMate2.Domain.Chat;
using ArquivoMate2.Domain.ReadModels;
using ArquivoMate2.Shared.Models;
using Marten;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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
        private readonly IDocumentSession _documentSession;

        public AskCatalogDocumentQuestionQueryHandler(
            IQuerySession querySession,
            IDocumentSession documentSession,
            IChatBot chatBot,
            ISearchClient searchClient,
            IFileMetadataService fileMetadataService,
            ILogger<AskCatalogDocumentQuestionQueryHandler> logger)
        {
            _querySession = querySession;
            _documentSession = documentSession;
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

            var historyEntries = request.Request.IncludeHistory
                ? await LoadHistoryAsync(request.UserId, cancellationToken)
                : Array.Empty<string>();

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
                History = historyEntries,
                Chunks = Array.Empty<DocumentChunk>(),
                SuggestedChunkIds = Array.Empty<string>()
            };

            var tooling = new DocumentQuestionTooling(request.UserId, null, _querySession, _searchClient, _fileMetadataService, _logger);

            var answer = await _chatBot.AnswerQuestion(context, request.Request.Question, tooling, cancellationToken);

            await StoreChatTurnAsync(request.UserId, request.Request.Question, answer, cancellationToken);

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

        private async Task<IReadOnlyList<string>> LoadHistoryAsync(string userId, CancellationToken cancellationToken)
        {
            try
            {
                var streamId = ChatStreamIdFactory.ForCatalog(userId);
                var events = await _querySession.Events.FetchStreamAsync(streamId, token: cancellationToken);
                return events
                    .Select(e => e.Data)
                    .OfType<CatalogChatTurnRecorded>()
                    .OrderByDescending(e => e.OccurredAt)
                    .Take(5)
                    .SelectMany(FormatHistoryEntry)
                    .Reverse()
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load catalog chat history for user {UserId}", userId);
                return Array.Empty<string>();
            }
        }

        private async Task StoreChatTurnAsync(string userId, string question, DocumentAnswerResult answer, CancellationToken cancellationToken)
        {
            try
            {
                var streamId = ChatStreamIdFactory.ForCatalog(userId);
                var citations = answer.Citations.Select(c => new DocumentChatCitation(c.Source, c.Snippet)).ToList();
                var documents = answer.Documents.Select(d => new DocumentChatReference(d.DocumentId, d.Title, d.Summary, d.Date, d.Score, d.FileSizeBytes)).ToList();

                _documentSession.Events.Append(streamId, new CatalogChatTurnRecorded(
                    userId,
                    question,
                    answer.Answer,
                    string.IsNullOrWhiteSpace(answer.Model) ? _chatBot.ModelName : answer.Model,
                    citations,
                    documents,
                    answer.DocumentCount,
                    DateTimeOffset.UtcNow));

                await _documentSession.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist catalog chat turn for user {UserId}", userId);
            }
        }

        private static IEnumerable<string> FormatHistoryEntry(CatalogChatTurnRecorded turn)
        {
            yield return $"User: {turn.Question}";
            if (!string.IsNullOrWhiteSpace(turn.Answer))
            {
                yield return $"Assistant: {turn.Answer}";
            }
        }
    }
}
