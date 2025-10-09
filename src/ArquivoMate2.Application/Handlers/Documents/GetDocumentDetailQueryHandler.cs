using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Queries.Documents;
using ArquivoMate2.Domain.ReadModels;
using ArquivoMate2.Shared.Models;
using AutoMapper;
using JasperFx.Events;
using Marten;
using Marten.Events;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Documents;

public class GetDocumentDetailQueryHandler : IRequestHandler<GetDocumentDetailQuery, DocumentDetailQueryResultDto?>
{
    private readonly IQuerySession _querySession;
    private readonly IDocumentAccessService _documentAccessService;
    private readonly IMapper _mapper;

    public GetDocumentDetailQueryHandler(IQuerySession querySession, IDocumentAccessService documentAccessService, IMapper mapper)
    {
        _querySession = querySession;
        _documentAccessService = documentAccessService;
        _mapper = mapper;
    }

    public async Task<DocumentDetailQueryResultDto?> Handle(GetDocumentDetailQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _documentAccessService.HasAccessToDocumentAsync(request.DocumentId, request.UserId, cancellationToken);
        if (!hasAccess)
        {
            return null;
        }

        var view = await _querySession.Query<DocumentView>()
            .Where(d => d.Id == request.DocumentId && !d.Deleted)
            .FirstOrDefaultAsyncFallback(cancellationToken);

        if (view is null)
        {
            return null;
        }

        var events = await _querySession.Events.FetchStreamAsync(request.DocumentId, token: cancellationToken);
        var history = MapHistory(events);

        var documentDto = _mapper.Map<DocumentDto>(view);
        documentDto.History = history;

        return new DocumentDetailQueryResultDto
        {
            Document = documentDto
        };
    }

    private static List<DocumentEventDto> MapHistory(IReadOnlyList<IEvent> events)
    {
        var history = new List<DocumentEventDto>(events.Count);
        foreach (var e in events)
        {
            var eventType = e.EventTypeName ?? e.GetType().Name;
            var occurredOn = TryGetOccurredOn(e) ?? e.Timestamp.UtcDateTime;
            var eventUserId = TryGetUserId(e);
            string? data = null;
            if (e.Data != null)
            {
                data = JsonSerializer.Serialize(e.Data);
            }

            history.Add(new DocumentEventDto
            {
                EventType = eventType,
                OccurredOn = occurredOn,
                UserId = eventUserId,
                Data = data
            });
        }

        return history;
    }

    private static DateTime? TryGetOccurredOn(IEvent e)
    {
        var property = e.Data?.GetType().GetProperty("OccurredOn", BindingFlags.Public | BindingFlags.Instance);
        return property?.GetValue(e.Data) as DateTime?;
    }

    private static string? TryGetUserId(IEvent e)
    {
        var property = e.Data?.GetType().GetProperty("UserId", BindingFlags.Public | BindingFlags.Instance);
        return property?.GetValue(e.Data)?.ToString();
    }
}
