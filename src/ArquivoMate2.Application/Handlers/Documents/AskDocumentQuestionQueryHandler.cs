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

namespace ArquivoMate2.Application.Handlers.Documents;

public class AskDocumentQuestionQueryHandler : IRequestHandler<AskDocumentQuestionQuery, DocumentAnswerDto?>
{
    private readonly IQuerySession _querySession;
    private readonly IDocumentAccessService _documentAccessService;
    private readonly IChatBot _chatBot;
    private readonly ILogger<AskDocumentQuestionQueryHandler> _logger;
    private readonly ISearchClient _searchClient;
    private readonly IFileMetadataService _fileMetadataService;
    private readonly IDocumentSession _documentSession;
    private readonly IDocumentVectorizationService _vectorizationService;

    public AskDocumentQuestionQueryHandler(
        IQuerySession querySession,
        IDocumentSession documentSession,
        IDocumentAccessService documentAccessService,
        IChatBot chatBot,
        ILogger<AskDocumentQuestionQueryHandler> logger,
        ISearchClient searchClient,
        IFileMetadataService fileMetadataService,
        IDocumentVectorizationService vectorizationService)
    {
        _querySession = querySession;
        _documentSession = documentSession;
        _documentAccessService = documentAccessService;
        _chatBot = chatBot;
        _logger = logger;
        _searchClient = searchClient;
        _fileMetadataService = fileMetadataService;
        _vectorizationService = vectorizationService;
    }

    public async Task<DocumentAnswerDto?> Handle(AskDocumentQuestionQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Request.Question))
        {
            throw new ArgumentException("Question must not be empty.", nameof(request));
        }

        var hasAccess = await _documentAccessService.HasAccessToDocumentAsync(request.DocumentId, request.UserId, cancellationToken);
        if (!hasAccess)
        {
            _logger.LogWarning("User {UserId} tried to access document {DocumentId} without permissions.", request.UserId, request.DocumentId);
            return null;
        }

        var view = await _querySession.Query<DocumentView>()
            .Where(d => d.Id == request.DocumentId && !d.Deleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (view is null)
        {
            return null;
        }

        var historyEntries = request.Request.IncludeHistory
            ? await LoadHistoryAsync(request.DocumentId, request.UserId, cancellationToken)
            : Array.Empty<string>();

        var chunks = DocumentChunking.Split(view.Content);
        IReadOnlyList<string> suggestedChunkIds = Array.Empty<string>();
        try
        {
            if (!string.IsNullOrWhiteSpace(request.Request.Question))
            {
                suggestedChunkIds = await _vectorizationService.FindRelevantChunkIdsAsync(
                    view.Id,
                    request.UserId,
                    request.Request.Question,
                    limit: 8,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vector search failed for document {DocumentId}", request.DocumentId);
        }

        var context = new DocumentQuestionContext
        {
            DocumentId = view.Id,
            Title = view.Title,
            Summary = view.Summary,
            Keywords = view.Keywords ?? new List<string>(),
            Content = view.Content,
            Language = view.Language,
            History = historyEntries,
            Chunks = chunks,
            SuggestedChunkIds = suggestedChunkIds
        };

        var tooling = new DocumentQuestionTooling(request.UserId, request.DocumentId, _querySession, _searchClient, _fileMetadataService, _logger);

        var answer = await _chatBot.AnswerQuestion(context, request.Request.Question, tooling, cancellationToken);

        await StoreChatTurnAsync(
            request.DocumentId,
            request.UserId,
            request.Request.Question,
            answer,
            cancellationToken);

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

    private async Task<IReadOnlyList<string>> LoadHistoryAsync(Guid documentId, string userId, CancellationToken cancellationToken)
    {
        try
        {
            var streamId = ChatStreamIdFactory.ForDocument(documentId, userId);
            var events = await _querySession.Events.FetchStreamAsync(streamId, token: cancellationToken);
            return events
                .Select(e => e.Data)
                .OfType<DocumentChatTurnRecorded>()
                .OrderByDescending(e => e.OccurredAt)
                .Take(5)
                .SelectMany(FormatHistoryEntry)
                .Reverse()
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load history for document {DocumentId}", documentId);
            return Array.Empty<string>();
        }
    }

    private async Task StoreChatTurnAsync(Guid documentId, string userId, string question, DocumentAnswerResult answer, CancellationToken cancellationToken)
    {
        try
        {
            var streamId = ChatStreamIdFactory.ForDocument(documentId, userId);
            var citations = answer.Citations.Select(c => new DocumentChatCitation(c.Source, c.Snippet)).ToList();
            var documents = answer.Documents.Select(d => new DocumentChatReference(d.DocumentId, d.Title, d.Summary, d.Date, d.Score, d.FileSizeBytes)).ToList();

            _documentSession.Events.Append(streamId, new DocumentChatTurnRecorded(
                documentId,
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
            _logger.LogWarning(ex, "Failed to persist chat turn for document {DocumentId}", documentId);
        }
    }

    private static IEnumerable<string> FormatHistoryEntry(DocumentChatTurnRecorded turn)
    {
        yield return $"User: {turn.Question}";
        if (!string.IsNullOrWhiteSpace(turn.Answer))
        {
            yield return $"Assistant: {turn.Answer}";
        }
    }
}
