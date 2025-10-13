using Amazon.Runtime.Internal;
using ArquivoMate2.Application.Commands;
using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.DTOs; // Ensure DTOs are available to the controller
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Queries.Documents;
using ArquivoMate2.Application.Services;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.Import;
using ArquivoMate2.Shared.Models;
using ArquivoMate2.Shared.ApiModels; // ApiResponse
using AutoMapper;
using Hangfire;
using Marten;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Linq;
using System.Threading;
using ArquivoMate2.Domain.ReadModels;
using System.Diagnostics;

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
        private readonly CustomEncryptionSettings _encryptionSettings; // Encryption feature configuration
        private readonly AppSettings _appSettings; // Global application configuration

        // ActivitySource for tracing this controller. OpenTelemetry will pick up activities started from this source.
        private static readonly ActivitySource s_activitySource = new("ArquivoMate2.DocumentsController", "1.0");

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentsController"/> class with the dependencies required to orchestrate document operations.
        /// </summary>
        public DocumentsController(
            IMediator mediator,
            IWebHostEnvironment env,
            ICurrentUserService currentUserService,
            IMapper mapper,
            IStringLocalizer<DocumentsController> localizer,
            IDocumentAccessService documentAccessService,
            IFileAccessTokenService tokenService,
            CustomEncryptionSettings encryptionSettings,
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
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<Guid>))]
        public async Task<ActionResult<ApiResponse<Guid>>> Upload([FromForm] UploadDocumentRequest request, CancellationToken cancellationToken, [FromServices] OcrSettings ocrSettings, [FromServices] IDocumentSession querySession)
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
        [HttpPatch("{id}/update-fields")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<DocumentDto>))]
        public async Task<ActionResult<ApiResponse<DocumentDto>>> UpdateFields(Guid id, [FromBody] UpdateDocumentFieldsDto dto, CancellationToken cancellationToken, [FromServices] IQuerySession querySession)
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
                        if (document!.Encryption == DocumentEncryptionType.Custom) ApplyDeliveryTokens(mapped);
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
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<DocumentListDto>))]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<ApiResponse<DocumentListDto>>> Get([FromQuery] DocumentListRequestDto requestDto,
            CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId;
            var result = await _mediator.Send(new GetDocumentListQuery(userId, requestDto), cancellationToken);

            var documents = result.Documents.Select(item =>
            {
                if (item.Encryption == DocumentEncryptionType.Custom && !string.IsNullOrEmpty(item.ThumbnailPath))
                {
                    // Use UploadedAt from list item as the stable anchor for token expiry
                    item.ThumbnailPath = BuildDeliveryUrl(item.Id, "thumb", item.UploadedAt);
                }
                return item;
            }).ToList();

            var dto = new DocumentListDto
            {
                Documents = documents,
                TotalCount = result.TotalCount,
                PageCount = result.PageCount,
                HasNextPage = result.HasNextPage,
                HasPreviousPage = result.HasPreviousPage,
                IsFirstPage = result.IsFirstPage,
                IsLastPage = result.IsLastPage,
                CurrentPage = result.CurrentPage
            };

            return Ok(dto);
        }

        /// <summary>
        /// Returns aggregate document statistics and search facets for the current user.
        /// </summary>
        [HttpGet("stats")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<DocumentStatsDto>))]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<ApiResponse<DocumentStatsDto>>> StatsAsync(CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId;
            var result = await _mediator.Send(new GetDocumentStatsQuery(userId), cancellationToken);

            var dto = new DocumentStatsDto
            {
                Id = Guid.Empty,
                Documents = result.Documents,
                NotAccepted = result.NotAccepted,
                Characters = result.Characters,
                Facets = result.Facets
            };

            return Ok(dto);
        }

        /// <summary>
        /// Retrieves a single document including its projection and event history.
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<DocumentDto>))]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<DocumentDto>>> Get(Guid id, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId;
            var result = await _mediator.Send(new GetDocumentDetailQuery(userId, id), cancellationToken);

            if (result?.Document is null)
            {
                return NotFound();
            }

            var documentDto = result.Document;
            if (documentDto.Encryption == DocumentEncryptionType.Custom)
            {
                ApplyDeliveryTokens(documentDto);
            }

            return Ok(documentDto);
        }

        /// <summary>
        /// Sends a user's natural language question to the configured chatbot and returns the generated answer.
        /// </summary>
        [HttpPost("{id:guid}/chat")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<DocumentAnswerDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<DocumentAnswerDto>>> AskQuestion(Guid id, [FromBody] DocumentQuestionRequestDto request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Question))
            {
                return BadRequest(_localizer.GetString("A question is required to query the chatbot.").Value);
            }

            var userId = _currentUserService.UserId;
            var answer = await _mediator.Send(new AskDocumentQuestionQuery(userId, id, request), cancellationToken);

            if (answer is null)
            {
                return NotFound();
            }

            return Ok(answer);
        }

        /// <summary>
        /// Sends a question about the user's entire document catalogue to the chatbot.
        /// </summary>
        [HttpPost("chat")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<DocumentAnswerDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<DocumentAnswerDto>>> AskCatalogQuestion([FromBody] DocumentQuestionRequestDto request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Question))
            {
                return BadRequest(_localizer.GetString("A question is required to query the chatbot.").Value);
            }

            var userId = _currentUserService.UserId;
            var answer = await _mediator.Send(new AskCatalogDocumentQuestionQuery(userId, request), cancellationToken);

            return Ok(answer);
        }

        /// <summary>
        /// Applies signed delivery URLs to encrypted document artifacts so they can be fetched securely.
        /// </summary>
        private void ApplyDeliveryTokens(DocumentDto dto)
        {
            // Compute a stable base timestamp for the token so the generated token remains identical
            // across multiple calls until the token expiry. Use ProcessedAt when available, otherwise UploadedAt.
            var baseTimestamp = dto.ProcessedAt ?? dto.UploadedAt;

            dto.FilePath = string.IsNullOrEmpty(dto.FilePath) ? dto.FilePath : BuildDeliveryUrl(dto, "file", baseTimestamp);
            dto.PreviewPath = string.IsNullOrEmpty(dto.PreviewPath) ? dto.PreviewPath : BuildDeliveryUrl(dto, "preview", baseTimestamp);
            dto.ThumbnailPath = string.IsNullOrEmpty(dto.ThumbnailPath) ? dto.ThumbnailPath : BuildDeliveryUrl(dto, "thumb", baseTimestamp);
            dto.MetadataPath = string.IsNullOrEmpty(dto.MetadataPath) ? dto.MetadataPath : BuildDeliveryUrl(dto, "metadata", baseTimestamp);
            dto.ArchivePath = string.IsNullOrEmpty(dto.ArchivePath) ? dto.ArchivePath : BuildDeliveryUrl(dto, "archive", baseTimestamp);
        }

        /// <summary>
        /// Builds a signed delivery URL for a specific document artifact.
        /// </summary>
        private string BuildDeliveryUrl(DocumentDto dto, string artifact, DateTime baseTimestamp)
        {
            // Deterministic expiry: anchor expiry to the document's processed/uploaded timestamp.
            // If the anchored expiry is already in the past, move to the next TTL window so the token stays valid
            // while remaining deterministic for the current window.
            var expires = ComputeWindowedExpiry(baseTimestamp);
            var token = _tokenService.Create(dto.Id, artifact, expires);
            var baseUrl = !string.IsNullOrWhiteSpace(_appSettings.PublicBaseUrl)
                ? _appSettings.PublicBaseUrl!.TrimEnd('/')
                : ($"{Request.Scheme}://{Request.Host}");
            return $"{baseUrl}/api/delivery/{dto.Id}/{artifact}?token={Uri.EscapeDataString(token)}";
        }

        // Overload for list mapping when only an Id and a base timestamp (UploadedAt) is available
        private string BuildDeliveryUrl(Guid documentId, string artifact, DateTime baseTimestamp)
        {
            var expires = ComputeWindowedExpiry(baseTimestamp);
            var token = _tokenService.Create(documentId, artifact, expires);
            var baseUrl = !string.IsNullOrWhiteSpace(_appSettings.PublicBaseUrl)
                ? _appSettings.PublicBaseUrl!.TrimEnd('/')
                : ($"{Request.Scheme}://{Request.Host}");
            return $"{baseUrl}/api/delivery/{documentId}/{artifact}?token={Uri.EscapeDataString(token)}";
        }

        // Compute an expiry anchored to baseTimestamp but ensure it is in the future by advancing
        // to the next token TTL window when necessary. This keeps tokens deterministic per window.
        private DateTimeOffset ComputeWindowedExpiry(DateTime baseTimestamp)
        {
            var anchor = DateTime.SpecifyKind(baseTimestamp, DateTimeKind.Utc);
            var windowSeconds = Math.Max(1, _encryptionSettings.TokenTtlMinutes) * 60L;
            var anchorUnix = new DateTimeOffset(anchor).ToUnixTimeSeconds();
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // initial candidate expiry = anchor + one window
            var expiresUnix = anchorUnix + windowSeconds;

            if (expiresUnix <= nowUnix)
            {
                // advance to the next window that lies in the future
                var windowsElapsed = (nowUnix - anchorUnix) / windowSeconds;
                expiresUnix = anchorUnix + (windowsElapsed + 1) * windowSeconds;
            }

            return DateTimeOffset.FromUnixTimeSeconds(expiresUnix);
        }

        /// <summary>
        /// Creates a time-limited external share link for a document artifact.
        /// </summary>
        [HttpPost("share")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ShareCreatedDto>))]
        public async Task<ActionResult<ApiResponse<ShareCreatedDto>>> CreateShare([FromBody] CreateShareRequest request, [FromServices] IExternalShareService shareService, [FromServices] AppSettings appSettings, CancellationToken ct)
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

