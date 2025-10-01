using Amazon.Runtime.Internal;
using ArquivoMate2.Application.Commands;
using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.DTOs; // ensure DTOs
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Services;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.Import;
using ArquivoMate2.Infrastructure.Persistance; // ensure access to DocumentAccessView & DocumentView
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
using System.Linq;
using System.Threading;
using ArquivoMate2.API.Querying; // neu

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
        private readonly IDocumentAccessService _documentAccessService;
        private readonly IFileAccessTokenService _tokenService; // NEW
        private readonly EncryptionSettings _encryptionSettings; // NEW
        private readonly AppSettings _appSettings; // NEW

        public DocumentsController(
            IMediator mediator,
            IWebHostEnvironment env,
            ICurrentUserService currentUserService,
            IMapper mapper,
            IStringLocalizer<DocumentsController> localizer,
            IDocumentAccessService documentAccessService,
            IFileAccessTokenService tokenService,
            EncryptionSettings encryptionSettings,
            AppSettings appSettings) // NEW
        {
            _mediator = mediator;
            _env = env;
            _currentUserService = currentUserService;
            _mapper = mapper;
            _localizer = localizer;
            _documentAccessService = documentAccessService;
            _tokenService = tokenService;
            _encryptionSettings = encryptionSettings;
            _appSettings = appSettings; // NEW
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<IActionResult> Upload([FromForm] UploadDocumentRequest request, CancellationToken cancellationToken, [FromServices] OcrSettings ocrSettings, [FromServices] IDocumentSession querySession)
        {
            if (request.File is null || request.File.Length == 0)
                return BadRequest();

            var historyEvent = new InitDocumentImport(
                Guid.NewGuid(),
                _currentUserService.UserId,
                request.File.FileName,
                DateTime.UtcNow,
                ImportSource.User);

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
            var document = await querySession.Query<DocumentView>()
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

                        document = await querySession.Query<DocumentView>()
                            .Where(d => d.Id == id && d.UserId == userId && !d.Deleted)
                            .FirstOrDefaultAsync(cancellationToken);
                        var mapped = _mapper.Map<DocumentDto>(document);
                        if (document!.Encrypted) ApplyDeliveryTokens(mapped);
                        return Ok(mapped);
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

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DocumentListDto))]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Get([FromQuery] DocumentListRequestDto requestDto,
            CancellationToken cancellationToken, [FromServices] IQuerySession querySession, [FromServices] ISearchClient searchClient)
        {
            var userId = _currentUserService.UserId;
            requestDto.NormalizePaging();

            IReadOnlyList<Guid>? searchIds = null;
            long? searchTotal = null;

            // Wenn Volltext vorhanden: zuerst Meili
            if (!string.IsNullOrWhiteSpace(requestDto.Search))
            {
                var (ids, total) = await searchClient.SearchDocumentIdsAsync(userId, requestDto.Search, requestDto.Page, requestDto.PageSize, cancellationToken);
                searchIds = ids;
                searchTotal = total;

                if (ids.Count == 0)
                {
                    return Ok(new DocumentListDto
                    {
                        Documents = new List<DocumentListItemDto>(),
                        TotalCount = 0,
                        PageCount = 0,
                        HasNextPage = false,
                        HasPreviousPage = false,
                        IsFirstPage = requestDto.Page == 1,
                        IsLastPage = true,
                        CurrentPage = requestDto.Page
                    });
                }
            }

            // Subquery for shared access (excluding own documents) - materialize to list to avoid passing IQueryable to parameter
            var sharedAccessibleIdsList = await querySession.Query<DocumentAccessView>()
                .Where(a => a.EffectiveUserIds.Contains(userId) && a.OwnerUserId != userId)
                .Select(a => a.Id)
                .ToListAsync(cancellationToken);

            IEnumerable<Guid>? sharedAccessibleIds = sharedAccessibleIdsList.Count > 0 ? sharedAccessibleIdsList : null;

            var baseQuery = querySession.Query<DocumentView>()
                .ApplyDocumentFilters(requestDto, userId, sharedAccessibleIds);

            if (searchIds != null)
            {
                // Eingrenzen auf Treffer aus Meili
                baseQuery = baseQuery.Where(d => searchIds.Contains(d.Id));
            }

            baseQuery = baseQuery.ApplySorting(requestDto);

            if (searchIds != null)
            {
                // Reihenfolge der Meili-Suche beibehalten
                var dict = searchIds.Select((id, idx) => new { id, idx }).ToDictionary(x => x.id, x => x.idx);
                var docs = await baseQuery.Where(d => searchIds.Contains(d.Id)).ToListAsync(cancellationToken);
                var ordered = docs.OrderBy(d => dict[d.Id]).ToList();

                var mapped = _mapper.Map<IList<DocumentListItemDto>>(ordered);
                // Replace thumbnail for encrypted docs
                for (int i = 0; i < ordered.Count; i++)
                {
                    if (ordered[i].Encrypted && !string.IsNullOrEmpty(mapped[i].ThumbnailPath))
                        mapped[i].ThumbnailPath = BuildDeliveryUrl(ordered[i].Id, "thumb");
                }
                var total = searchTotal ?? mapped.Count;
                var pageCount = (int)Math.Ceiling(total / (double)requestDto.PageSize);

                return Ok(new DocumentListDto
                {
                    Documents = mapped,
                    TotalCount = total,
                    PageCount = pageCount,
                    HasNextPage = requestDto.Page < pageCount,
                    HasPreviousPage = requestDto.Page > 1,
                    IsFirstPage = requestDto.Page == 1,
                    IsLastPage = requestDto.Page >= pageCount,
                    CurrentPage = requestDto.Page
                });
            }
            else
            {
                var paged = await baseQuery.ToPagedListAsync(requestDto.Page, requestDto.PageSize, cancellationToken);
                var mapped = paged.Count == 0 ? new List<DocumentListItemDto>() : _mapper.Map<IList<DocumentListItemDto>>(paged);
                for (int i = 0; i < paged.Count; i++)
                {
                    if (paged[i].Encrypted && !string.IsNullOrEmpty(mapped[i].ThumbnailPath))
                        mapped[i].ThumbnailPath = BuildDeliveryUrl(paged[i].Id, "thumb");
                }

                var dto = new DocumentListDto
                {
                    Documents = mapped,
                    TotalCount = paged.TotalItemCount,
                    HasNextPage = paged.HasNextPage,
                    HasPreviousPage = paged.HasPreviousPage,
                    PageCount = paged.PageCount,
                    IsFirstPage = paged.IsFirstPage,
                    IsLastPage = paged.IsLastPage,
                    CurrentPage = requestDto.Page
                };

                return Ok(dto);
            }
        }

        [HttpGet("stats")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DocumentStatsDto))]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> StatsAsync(CancellationToken cancellationToken, [FromServices] IQuerySession querySession, [FromServices] ISearchClient searchClient)
        {
            var userId = _currentUserService.UserId;

            var sharedAccessibleIdsList = await querySession.Query<DocumentAccessView>()
                .Where(a => a.EffectiveUserIds.Contains(userId) && a.OwnerUserId != userId)
                .Select(a => a.Id)
                .ToListAsync(cancellationToken);

            var accessibleQuery = querySession.Query<DocumentView>()
                .Where(d => !d.Deleted && (d.UserId == userId || sharedAccessibleIdsList.Contains(d.Id)));

            var count = await accessibleQuery.CountAsync(cancellationToken);
            var notAccepted = await accessibleQuery.Where(d => !d.Accepted).CountAsync(cancellationToken);
            var characters = await accessibleQuery.SumAsync(d => d.ContentLength, cancellationToken);

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

            var hasAccess = await _documentAccessService.HasAccessToDocumentAsync(id, userId, cancellationToken);
            if (!hasAccess)
            {
                return NotFound();
            }

            var view = await querySession.Query<DocumentView>()
                .Where(d => d.Id == id && !d.Deleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (view is null)
                return NotFound();

            var events = await querySession.Events.FetchStreamAsync(id, token: cancellationToken);
            var documentDto = _mapper.Map<DocumentDto>(view);
            if (view.Encrypted) ApplyDeliveryTokens(documentDto);

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

        private void ApplyDeliveryTokens(DocumentDto dto)
        {
            dto.FilePath = string.IsNullOrEmpty(dto.FilePath) ? dto.FilePath : BuildDeliveryUrl(dto.Id, "file");
            dto.PreviewPath = string.IsNullOrEmpty(dto.PreviewPath) ? dto.PreviewPath : BuildDeliveryUrl(dto.Id, "preview");
            dto.ThumbnailPath = string.IsNullOrEmpty(dto.ThumbnailPath) ? dto.ThumbnailPath : BuildDeliveryUrl(dto.Id, "thumb");
            dto.MetadataPath = string.IsNullOrEmpty(dto.MetadataPath) ? dto.MetadataPath : BuildDeliveryUrl(dto.Id, "metadata");
            dto.ArchivePath = string.IsNullOrEmpty(dto.ArchivePath) ? dto.ArchivePath : BuildDeliveryUrl(dto.Id, "archive");
        }

        private string BuildDeliveryUrl(Guid documentId, string artifact)
        {
            var expires = DateTimeOffset.UtcNow.AddMinutes(_encryptionSettings.TokenTtlMinutes);
            var token = _tokenService.Create(documentId, artifact, expires);
            var baseUrl = !string.IsNullOrWhiteSpace(_appSettings.PublicBaseUrl)
                ? _appSettings.PublicBaseUrl!.TrimEnd('/')
                : ($"{Request.Scheme}://{Request.Host}");
            return $"{baseUrl}/api/delivery/{documentId}/{artifact}?token={Uri.EscapeDataString(token)}";
        }

        [HttpPost("share")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ShareCreatedDto))]
        public async Task<IActionResult> CreateShare([FromBody] CreateShareRequest request, [FromServices] IExternalShareService shareService, [FromServices] AppSettings appSettings, CancellationToken ct)
        {
            if (request == null || request.DocumentId == Guid.Empty)
                return BadRequest();
            var artifact = string.IsNullOrWhiteSpace(request.Artifact) ? "file" : request.Artifact.Trim().ToLowerInvariant();
            var allowed = new[] { "file", "preview", "thumb", "metadata", "archive" };
            if (!allowed.Contains(artifact)) return BadRequest("Invalid artifact");

            var hasAccess = await _documentAccessService.HasAccessToDocumentAsync(request.DocumentId, _currentUserService.UserId, ct);
            if (!hasAccess) return NotFound();

            int ttlReq = request.TtlMinutes ?? appSettings.PublicShareDefaultTtlMinutes;
            if (ttlReq <= 0) ttlReq = appSettings.PublicShareDefaultTtlMinutes;
            if (ttlReq > appSettings.PublicShareMaxTtlMinutes) ttlReq = appSettings.PublicShareMaxTtlMinutes;
            var ttl = TimeSpan.FromMinutes(ttlReq);

            var share = await shareService.CreateAsync(request.DocumentId, _currentUserService.UserId, artifact, ttl, ct);
            var token = _tokenService.CreateShareToken(share.Id, share.ExpiresAtUtc);
            var baseUrl = !string.IsNullOrWhiteSpace(appSettings.PublicBaseUrl) ? appSettings.PublicBaseUrl.TrimEnd('/') : ($"{Request.Scheme}://{Request.Host}");
            var url = $"{baseUrl}/api/share/{share.Id}?token={Uri.EscapeDataString(token)}";

            return Ok(new ShareCreatedDto
            {
                ShareId = share.Id,
                Artifact = artifact,
                ExpiresAtUtc = share.ExpiresAtUtc,
                Url = url
            });
        }
    }
}

