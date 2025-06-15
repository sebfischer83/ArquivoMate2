using Amazon.Runtime.Internal;
using ArquivoMate2.Application.Commands;
using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Services;
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

        public DocumentsController(IMediator mediator, IWebHostEnvironment env, ICurrentUserService currentUserService, IMapper mapper)
        {
            _mediator = mediator;
            _env = env;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }

        [HttpGet("pending")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<DocumentStatusDto>))]
        public async Task<IActionResult> GetPendingDocuments(CancellationToken cancellationToken, [FromServices] IQuerySession querySession)
        {
            var userId = _currentUserService.UserId;

            var pendingDocuments = await querySession.Query<Infrastructure.Persistance.DocumentView>()
                .Where(doc => (doc.Status != ProcessingStatus.Completed && doc.Status != ProcessingStatus.Failed) && doc.UserId == userId)
                .ToListAsync(cancellationToken);

            var result = pendingDocuments.Select(doc => new DocumentStatusDto
            {
                DocumentId = doc.Id,
                Status = doc.Status,
                UploadedAt = doc.UploadedAt
            }).ToList();

            return Ok(result);
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<IActionResult> Upload([FromForm] UploadDocumentRequest request, CancellationToken cancellationToken, [FromServices] OcrSettings ocrSettings)
        {
            if (request.File is null || request.File.Length == 0)
                return BadRequest();

            var id = await _mediator.Send(new UploadDocumentCommand(request), cancellationToken);

            BackgroundJob.Enqueue<DocumentProcessingService>("documents", svc => svc.ProcessAsync(id, _currentUserService.UserId));

            return CreatedAtAction(nameof(Upload), new { id }, id);
        }

        [HttpPatch("{id}/update-fields")]
        public async Task<IActionResult> UpdateFields(Guid id, [FromBody] JObject json, CancellationToken cancellationToken, [FromServices] IQuerySession querySession)
        {
            if (json == null || !json.Properties().Any())
                return BadRequest("No fields provided.");

            // Prüfen, ob das Dokument existiert
            var userId = _currentUserService.UserId;
            var document = await querySession.LoadAsync<Infrastructure.Persistance.DocumentView>(id, cancellationToken);
            if (document == null)
                return NotFound($"Document with ID {id} was not found.");

            if (document.UserId != userId)
                return Forbid();

            try
            {
                var result = await _mediator.Send(new UpdateDocumentCommand(id, json), cancellationToken);
            
                switch (result)
                {
                    case PatchResult.Success:
                        document = await querySession.LoadAsync<Infrastructure.Persistance.DocumentView>(id, cancellationToken);
                        return Ok("Fields updated successfully.");
                    case PatchResult.Forbidden:
                        return Forbid("Some fields are not allowed to update.");
                    case PatchResult.Failed:
                        return BadRequest("Failed to update fields. Please try again.");
                    case PatchResult.Invalid:
                        return BadRequest("Invalid fields provided for update.");
                    default:
                        return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
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
            var view = await querySession.Query<Infrastructure.Persistance.DocumentView>().Where(d => d.UserId == userId).
                ToPagedListAsync(requestDto.Page, requestDto.PageSize);
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

            var count = await querySession.Query<DocumentView>().Where(d => d.UserId == userId).CountAsync(cancellationToken);
            var notAccepted = await querySession.Query<DocumentView>().Where(d => d.UserId == userId && !d.Accepted ).CountAsync(cancellationToken);
            var characters = await querySession.Query<DocumentView>().Where(d => d.UserId == userId).SumAsync(d => d.ContentLength, cancellationToken);

            var facets = await searchClient.GetFacetsAsync(userId, cancellationToken);

            //var x = await searchClient.ListUserIdsAsync(cancellationToken);

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
            var view = await querySession.LoadAsync<Infrastructure.Persistance.DocumentView>(id, cancellationToken);
            if (view is null)
                return NotFound();

            if (view.UserId != userId)
                return Forbid();

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

