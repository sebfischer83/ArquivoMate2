﻿using Amazon.Runtime.Internal;
using ArquivoMate2.Application.Commands;
using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Services;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.Import;
using ArquivoMate2.Infrastructure.Persistance;
using ArquivoMate2.Shared.Models;
using AutoMapper;
using Hangfire;
using Marten;
using Marten.Pagination;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Newtonsoft.Json.Linq;
using System.Threading;
using Weasel.Postgresql.Views;

namespace ArquivoMate2.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/documents")]
    public class DocumentsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IWebHostEnvironment _env;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;
        private readonly IStringLocalizer<DocumentsController> _localizer;

        public DocumentsController(
            IMediator mediator, 
            IWebHostEnvironment env, 
            ICurrentUserService currentUserService, 
            IMapper mapper,
            IStringLocalizer<DocumentsController> localizer)
        {
            _mediator = mediator;
            _env = env;
            _currentUserService = currentUserService;
            _mapper = mapper;
            _localizer = localizer;
        }

      
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<IActionResult> Upload([FromForm] UploadDocumentRequest request, CancellationToken cancellationToken, [FromServices] OcrSettings ocrSettings, [FromServices] IDocumentSession querySession)
        {
            if (request.File is null || request.File.Length == 0)
                return BadRequest();

            // Create InitDocumentImport event with explicit ImportSource.User for UI uploads
            var historyEvent = new InitDocumentImport(
                Guid.NewGuid(), 
                _currentUserService.UserId, 
                request.File.FileName, 
                DateTime.UtcNow, 
                ImportSource.User); // Explicitly set source as User for manual uploads

            querySession.Events.StartStream<ImportProcess>(historyEvent.AggregateId, historyEvent);
            await querySession.SaveChangesAsync();

            var id = await _mediator.Send(new UploadDocumentCommand(request), cancellationToken);

            BackgroundJob.Enqueue<DocumentProcessingService>("documents", svc => svc.ProcessAsync(id, historyEvent.AggregateId, _currentUserService.UserId));

            return CreatedAtAction(nameof(Upload), new { id }, id);
        }

        [HttpPatch("{id}/update-fields")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DocumentDto))]
        public async Task<IActionResult> UpdateFields(Guid id, [FromBody] UpdateDocumentFieldsDto dto, CancellationToken cancellationToken, [FromServices] IQuerySession querySession)
        {
            if (dto == null || !dto.Fields.Any())
                return BadRequest(_localizer.GetString("No fields provided.").Value);

            // Prüfen, ob das Dokument existiert und nicht gelöscht ist
            var userId = _currentUserService.UserId;
            var document = await querySession.Query<Infrastructure.Persistance.DocumentView>()
                .Where(d => d.Id == id && d.UserId == userId && !d.Deleted)
                .FirstOrDefaultAsync(cancellationToken);
            
            if (document == null)
                return NotFound(_localizer.GetString("Document with ID {0} was not found.", id).Value);

            try
            {
                var result = await _mediator.Send(new UpdateDocumentCommand(id, dto), cancellationToken);

                switch (result)
                {
                    case PatchResult.Success:
                        var rawDoc = await querySession.Events.AggregateStreamAsync<Document>(id);
                        // update index
                        await _mediator.Send(new UpdateIndexCommand(id, rawDoc!), cancellationToken);

                        document = await querySession.Query<Infrastructure.Persistance.DocumentView>()
                            .Where(d => d.Id == id && d.UserId == userId && !d.Deleted)
                            .FirstOrDefaultAsync(cancellationToken);
                        return Ok(_mapper.Map<DocumentDto>(document));
                    case PatchResult.Forbidden:
                        return Forbid(_localizer.GetString("Some fields are not allowed to update.").Value);
                    case PatchResult.Failed:
                        return BadRequest(_localizer.GetString("Failed to update fields. Please try again.").Value);
                    case PatchResult.Invalid:
                        return BadRequest(_localizer.GetString("Invalid fields provided for update.").Value);
                    default:
                        return StatusCode(StatusCodes.Status500InternalServerError, _localizer.GetString("An unexpected error occurred.").Value);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet()]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DocumentListDto))]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get([FromQuery] DocumentListRequestDto requestDto,
            CancellationToken cancellationToken, [FromServices] IQuerySession querySession)
        {
            var userId = _currentUserService.UserId;
            var view = await querySession.Query<Infrastructure.Persistance.DocumentView>()
                .Where(d => d.UserId == userId && d.Processed == true && !d.Deleted)
                .ToPagedListAsync(requestDto.Page, requestDto.PageSize);
            if (view is null)
                return NotFound();
            if (view.Count == 0)
                return NotFound();


            DocumentListDto documentListDto = new();
            var documents = _mapper.Map<DocumentListItemDto[]>(view);
            documentListDto.Documents = documents;

            documentListDto.TotalCount = view.TotalItemCount;
            documentListDto.HasNextPage = view.HasNextPage;
            documentListDto.PageCount = view.PageCount;
            documentListDto.HasPreviousPage = view.HasPreviousPage;
            documentListDto.IsLastPage = view.IsLastPage;
            documentListDto.IsFirstPage = view.IsFirstPage;
            documentListDto.CurrentPage = requestDto.Page;

            return Ok(documentListDto);

        }

        [HttpGet("stats")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DocumentStatsDto))]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> StatsAsync(CancellationToken cancellationToken, [FromServices] IQuerySession querySession, [FromServices] ISearchClient searchClient)
        {
            var userId = _currentUserService.UserId;

            var count = await querySession.Query<DocumentView>().Where(d => d.UserId == userId && !d.Deleted).CountAsync(cancellationToken);
            var notAccepted = await querySession.Query<DocumentView>().Where(d => d.UserId == userId && !d.Accepted && !d.Deleted).CountAsync(cancellationToken);
            var characters = await querySession.Query<DocumentView>().Where(d => d.UserId == userId && !d.Deleted).SumAsync(d => d.ContentLength, cancellationToken);

            var facets = await searchClient.GetFacetsAsync(userId, cancellationToken);

            DocumentStatsDto stats = new DocumentStatsDto
            {
                Id = Guid.Empty,
                Documents = count,
                NotAccepted = notAccepted,
                Characters = characters,
                Facets = facets
            };

            return Ok(stats);
        }



        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DocumentDto))]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken, [FromServices] IQuerySession querySession)
        {
            var userId = _currentUserService.UserId;
            var view = await querySession.Query<Infrastructure.Persistance.DocumentView>()
                .Where(d => d.Id == id && d.UserId == userId && !d.Deleted)
                .FirstOrDefaultAsync(cancellationToken);
            
            if (view is null)
                return NotFound();

            var events = await querySession.Events.FetchStreamAsync(id, token: cancellationToken);
            var documentDto = _mapper.Map<DocumentDto>(view);

            // Historie mappen
            var history = new List<DocumentEventDto>();
            foreach (var e in events)
            {
                var eventType = e.EventTypeName ?? e.GetType().Name;
                var occurredOn = (DateTime?)e.Data?.GetType().GetProperty("OccurredOn")?.GetValue(e.Data) ?? e.Timestamp.UtcDateTime;
                string? eventUserId = e.Data?.GetType().GetProperty("UserId")?.GetValue(e.Data)?.ToString();
                string? data = System.Text.Json.JsonSerializer.Serialize(e.Data);
                history.Add(new DocumentEventDto
                {
                    EventType = eventType,
                    OccurredOn = occurredOn,
                    UserId = eventUserId,
                    Data = data
                });
            }
            documentDto.History = history;

            return Ok(documentDto);
        }
    }
}

