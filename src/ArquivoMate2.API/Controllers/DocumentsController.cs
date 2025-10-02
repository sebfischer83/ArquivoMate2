using Amazon.Runtime.Internal;
using ArquivoMate2.Application.Commands;
using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.DTOs; // Ensure DTOs are available to the controller
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Services;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.Import;
using ArquivoMate2.Infrastructure.Persistance; // Access to DocumentAccessView & DocumentView projections
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
using ArquivoMate2.API.Querying; // Custom query extensions for filtering and sorting

namespace ArquivoMate2.API.Controllers
{
    /// <summary>
    /// Provides endpoints for managing document metadata, importing files, and retrieving document content.
    /// </summary>
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
        private readonly IFileAccessTokenService _tokenService; // Issues signed delivery tokens
        private readonly EncryptionSettings _encryptionSettings; // Encryption feature configuration
        private readonly AppSettings _appSettings; // Global application configuration

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentsController"/> class with the dependencies required to orchestrate document operations.
        /// </summary>
        /// <param name="mediator">Mediator used to dispatch commands.</param>
        /// <param name="env">Host environment describing the application context.</param>
        /// <param name="currentUserService">Service that exposes the current user's identity.</param>
        /// <param name="mapper">Mapper used to translate read models to DTOs.</param>
        /// <param name="localizer">Localization provider for user-facing messages.</param>
        /// <param name="documentAccessService">Service that validates document access rules.</param>
        /// <param name="tokenService">Token generator for secure delivery URLs.</param>
        /// <param name="encryptionSettings">Encryption settings used for token expiry.</param>
        /// <param name="appSettings">Application-wide configuration options.</param>
        public DocumentsController(
            IMediator mediator,
            IWebHostEnvironment env,
            ICurrentUserService currentUserService,
            IMapper mapper,
            IStringLocalizer<DocumentsController> localizer,
            IDocumentAccessService documentAccessService,
            IFileAccessTokenService tokenService,
            EncryptionSettings encryptionSettings,
            AppSettings appSettings)
        {
            _mediator = mediator;
            _env = env;
            _currentUserService = currentUserService;
            _mapper = mapper;
            _localizer = localizer;
            _documentAccessService = documentAccessService;
            _tokenService = tokenService;
            _encryptionSettings = encryptionSettings;
            _appSettings = appSettings;
        }

        /// <summary>
        /// Uploads a new document, starts its import stream, and schedules the background processing workflow.
        /// </summary>
        /// <param name="request">Form payload containing the file to upload.</param>
        /// <param name="cancellationToken">Cancellation token propagated from the HTTP request.</param>
        /// <param name="ocrSettings">OCR configuration required by the processing pipeline.</param>
        /// <param name="querySession">Document session used to persist the import start event.</param>
        /// <returns>A <see cref="CreatedAtActionResult"/> containing the newly created document identifier.</returns>
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

        /// <summary>
        /// Updates mutable document metadata fields and refreshes the search index when necessary.
        /// </summary>
        /// <param name="id">Identifier of the document whose fields should be updated.</param>
        /// <param name="dto">Set of field updates to apply.</param>
        /// <param name="cancellationToken">Cancellation token propagated from the HTTP request.</param>
        /// <param name="querySession">Query session used to load projections for validation and response construction.</param>
        /// <returns>The updated document representation or an appropriate error response.</returns>
        [HttpPatch("{id}/update-fields")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DocumentDto))]
        public async Task<IActionResult> UpdateFields(Guid id, [FromBody] UpdateDocumentFieldsDto dto, CancellationToken cancellationToken, [FromServices] IQuerySession querySession)
        {
            if (dto == null || !dto.Fields.Any())
                return BadRequest(_localizer.GetString("No fields provided.").Value);

            var userId = _currentUserService.UserId;
            var document = await querySession.Query<DocumentView>()
                .Where(d => d.Id == id && !d.Deleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (document == null)
                return NotFound(_localizer.GetString("Document with ID {0} was not found.", id).Value);

            var hasEditAccess = await _documentAccessService.HasEditAccessToDocumentAsync(id, userId, cancellationToken);
            if (!hasEditAccess)
            {
                return NotFound(_localizer.GetString("Document with ID {0} was not found.", id).Value);
            }

            try
            {
                var result = await _mediator.Send(new UpdateDocumentCommand(id, dto), cancellationToken);

                switch (result)
                {
                    case PatchResult.Success:
                        var rawDoc = await querySession.Events.AggregateStreamAsync<Document>(id);
                        // Update the search index with the new document values
                        await _mediator.Send(new UpdateIndexCommand(id, rawDoc!), cancellationToken);

                        document = await querySession.Query<DocumentView>()
                            .Where(d => d.Id == id && !d.Deleted)
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

        /// <summary>
        /// Retrieves a paginated document list using optional full-text search, filtering, and sorting criteria.
        /// </summary>
        /// <param name="requestDto">Query parameters describing paging, filters, and sorting.</param>
        /// <param name="cancellationToken">Cancellation token propagated from the HTTP request.</param>
        /// <param name="querySession">Query session used to compose the Marten query.</param>
        /// <param name="searchClient">Search client used to resolve full-text document hits.</param>
        /// <returns>A paged list of documents visible to the current user.</returns>
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

            // Run a full-text search first when a search term is provided
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
                // Restrict the result set to the Meilisearch hits
                baseQuery = baseQuery.Where(d => searchIds.Contains(d.Id));
            }

            baseQuery = baseQuery.ApplySorting(requestDto);

            if (searchIds != null)
            {
                // Preserve the ordering provided by Meilisearch
                var dict = searchIds.Select((id, idx) => new { id, idx }).ToDictionary(x => x.id, x => x.idx);
                // Materialize the query here so that we can reapply the search ranking order afterwards
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
                // No search term was used, so rely on Marten's pagination to fetch the current page only
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

        /// <summary>
        /// Returns aggregate document statistics and search facets for the current user.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token propagated from the HTTP request.</param>
        /// <param name="querySession">Query session used to count documents.</param>
        /// <param name="searchClient">Search client used to retrieve facet information.</param>
        /// <returns>A <see cref="DocumentStatsDto"/> representing the user's library statistics.</returns>
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

        /// <summary>
        /// Retrieves a single document including its projection and event history.
        /// </summary>
        /// <param name="id">Identifier of the document to retrieve.</param>
        /// <param name="cancellationToken">Cancellation token propagated from the HTTP request.</param>
        /// <param name="querySession">Query session used to load the document projection and event stream.</param>
        /// <returns>The requested document if available to the current user.</returns>
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

        /// <summary>
        /// Applies signed delivery URLs to encrypted document artifacts so they can be fetched securely.
        /// </summary>
        /// <param name="dto">Document DTO whose artifact paths should be replaced with secure URLs.</param>
        private void ApplyDeliveryTokens(DocumentDto dto)
        {
            dto.FilePath = string.IsNullOrEmpty(dto.FilePath) ? dto.FilePath : BuildDeliveryUrl(dto.Id, "file");
            dto.PreviewPath = string.IsNullOrEmpty(dto.PreviewPath) ? dto.PreviewPath : BuildDeliveryUrl(dto.Id, "preview");
            dto.ThumbnailPath = string.IsNullOrEmpty(dto.ThumbnailPath) ? dto.ThumbnailPath : BuildDeliveryUrl(dto.Id, "thumb");
            dto.MetadataPath = string.IsNullOrEmpty(dto.MetadataPath) ? dto.MetadataPath : BuildDeliveryUrl(dto.Id, "metadata");
            dto.ArchivePath = string.IsNullOrEmpty(dto.ArchivePath) ? dto.ArchivePath : BuildDeliveryUrl(dto.Id, "archive");
        }

        /// <summary>
        /// Builds a signed delivery URL for a specific document artifact.
        /// </summary>
        /// <param name="documentId">Document identifier that owns the artifact.</param>
        /// <param name="artifact">Artifact name (file, preview, thumb, metadata, archive).</param>
        /// <returns>A fully-qualified URL containing a time-limited signature.</returns>
        private string BuildDeliveryUrl(Guid documentId, string artifact)
        {
            var expires = DateTimeOffset.UtcNow.AddMinutes(_encryptionSettings.TokenTtlMinutes);
            var token = _tokenService.Create(documentId, artifact, expires);
            var baseUrl = !string.IsNullOrWhiteSpace(_appSettings.PublicBaseUrl)
                ? _appSettings.PublicBaseUrl!.TrimEnd('/')
                : ($"{Request.Scheme}://{Request.Host}");
            return $"{baseUrl}/api/delivery/{documentId}/{artifact}?token={Uri.EscapeDataString(token)}";
        }

        /// <summary>
        /// Creates a time-limited external share link for a document artifact.
        /// </summary>
        /// <param name="request">Request describing the document, artifact, and TTL.</param>
        /// <param name="shareService">Service responsible for persisting share definitions.</param>
        /// <param name="appSettings">Application settings used to enforce TTL limits.</param>
        /// <param name="ct">Cancellation token propagated from the HTTP request.</param>
        /// <returns>Information about the created share including its public URL.</returns>
        [HttpPost("share")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ShareCreatedDto))]
        public async Task<IActionResult> CreateShare([FromBody] CreateShareRequest request, [FromServices] IExternalShareService shareService, [FromServices] AppSettings appSettings, CancellationToken ct)
        {
            if (request == null || request.DocumentId == Guid.Empty)
                return BadRequest();

            var artifactEnum = request.Artifact ?? DocumentArtifact.File;
            var artifact = artifactEnum.ToWireValue();

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

