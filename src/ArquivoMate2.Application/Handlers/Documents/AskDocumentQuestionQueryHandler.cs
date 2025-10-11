using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;
using ArquivoMate2.Application.Queries.Documents;
using ArquivoMate2.Domain.ReadModels;
using ArquivoMate2.Shared.Models;
using Marten;
using Marten.Events;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Handlers.Documents;

public class AskDocumentQuestionQueryHandler : IRequestHandler<AskDocumentQuestionQuery, DocumentAnswerDto?>
{
    private readonly IQuerySession _querySession;
    private readonly IDocumentAccessService _documentAccessService;
    private readonly IChatBot _chatBot;
    private readonly ILogger<AskDocumentQuestionQueryHandler> _logger;

    public AskDocumentQuestionQueryHandler(
        IQuerySession querySession,
        IDocumentAccessService documentAccessService,
        IChatBot chatBot,
        ILogger<AskDocumentQuestionQueryHandler> logger)
    {
        _querySession = querySession;
        _documentAccessService = documentAccessService;
        _chatBot = chatBot;
        _logger = logger;
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
            ? await LoadHistoryAsync(request.DocumentId, cancellationToken)
            : Array.Empty<string>();

        var context = new DocumentQuestionContext
        {
            DocumentId = view.Id,
            Title = view.Title,
            Summary = view.Summary,
            Keywords = view.Keywords ?? new List<string>(),
            Content = view.Content,
            Language = view.Language,
            History = historyEntries
        };

        var answer = await _chatBot.AnswerQuestion(context, request.Request.Question, cancellationToken);

        return new DocumentAnswerDto
        {
            Answer = answer.Answer,
            Model = string.IsNullOrWhiteSpace(answer.Model) ? _chatBot.ModelName : answer.Model,
            Citations = answer.Citations.Select(c => new DocumentAnswerCitationDto
            {
                Source = c.Source,
                Snippet = c.Snippet
            }).ToList()
        };
    }

    private async Task<IReadOnlyList<string>> LoadHistoryAsync(Guid documentId, CancellationToken cancellationToken)
    {
        try
        {
            var events = await _querySession.Events.FetchStreamAsync(documentId, token: cancellationToken);
            return events
                .OrderByDescending(e => e.Timestamp)
                .Take(10)
                .Select(FormatHistoryEntry)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load history for document {DocumentId}", documentId);
            return Array.Empty<string>();
        }
    }

    private static string FormatHistoryEntry(IEvent e)
    {
        var eventName = e.EventTypeName ?? e.Data?.GetType().Name ?? "UnknownEvent";
        var occurredOn = e.Timestamp.UtcDateTime;
        var payload = e.Data != null ? JsonSerializer.Serialize(e.Data) : null;
        return payload is null
            ? $"{occurredOn:u} - {eventName}"
            : $"{occurredOn:u} - {eventName}: {payload}";
    }
}
