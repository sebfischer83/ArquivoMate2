using ArquivoMate2.Application.Commands;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Queries.ImportHistory;
using ArquivoMate2.Shared.ApiModels;
using ArquivoMate2.Shared.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace ArquivoMate2.API.Controllers;

[ApiController]
[Authorize]
[Route("api/history")]
public class ImportHistoryController : ControllerBase
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ImportHistoryController> _logger;
    private readonly IStringLocalizer<ImportHistoryController> _stringLocalizer;
    private readonly IMediator _mediator;

    public ImportHistoryController(
        ICurrentUserService currentUserService,
        ILogger<ImportHistoryController> logger,
        IStringLocalizer<ImportHistoryController> stringLocalizer,
        IMediator mediator)
    {
        _currentUserService = currentUserService;
        _logger = logger;
        _stringLocalizer = stringLocalizer;
        _mediator = mediator;
    }

    /// <summary>
    /// Hides every import history entry that matches the supplied processing status for the current user.
    /// </summary>
    [HttpPost("hideByStatus")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> HideAllFromImportHistory(DocumentProcessingStatus documentProcessingStatus, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        var success = await _mediator.Send(new HideAllFromImportHistoryByStatusCommand(documentProcessingStatus, userId), cancellationToken);
        if (!success)
        {
            _logger.LogError("Failed to hide import history entries with status {Status} for user {UserId}", documentProcessingStatus, userId);
            return StatusCode(StatusCodes.Status500InternalServerError, _stringLocalizer["Failed to hide import history entries."]);
        }
        return NoContent();
    }

    /// <summary>
    /// Retrieves a paged list of import history entries for the current user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ImportHistoryListDto>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<ImportHistoryListDto>>> Get([FromQuery] ImportHistoryListRequestDto requestDto, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        var dto = await _mediator.Send(new GetImportHistoryListQuery(requestDto.Page, requestDto.PageSize, userId, null), cancellationToken);
        return Ok(dto);
    }

    /// <summary>
    /// Counts all imports that are currently being processed for the current user.
    /// </summary>
    [HttpGet("inprogress/count")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<int>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<int>>> GetInProgressCount(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        var count = await _mediator.Send(new GetImportHistoryCountQuery(userId, DocumentProcessingStatus.InProgress), cancellationToken);
        return Ok(count);
    }

    /// <summary>
    /// Lists imports that are currently in progress for the current user.
    /// </summary>
    [HttpGet("inprogress")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ImportHistoryListDto>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<ImportHistoryListDto>>> GetInProgress([FromQuery] ImportHistoryListRequestDto requestDto, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        var dto = await _mediator.Send(new GetImportHistoryListQuery(requestDto.Page, requestDto.PageSize, userId, DocumentProcessingStatus.InProgress), cancellationToken);
        return Ok(dto);
    }

    /// <summary>
    /// Counts pending imports that are queued for processing.
    /// </summary>
    [HttpGet("pending/count")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<int>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<int>>> GetPendingCount(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        var count = await _mediator.Send(new GetImportHistoryCountQuery(userId, DocumentProcessingStatus.Pending), cancellationToken);
        return Ok(count);
    }

    /// <summary>
    /// Lists pending imports that are waiting to be processed.
    /// </summary>
    [HttpGet("pending")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ImportHistoryListDto>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<ImportHistoryListDto>>> GetPending([FromQuery] ImportHistoryListRequestDto requestDto, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        var dto = await _mediator.Send(new GetImportHistoryListQuery(requestDto.Page, requestDto.PageSize, userId, DocumentProcessingStatus.Pending), cancellationToken);
        return Ok(dto);
    }

    /// <summary>
    /// Counts completed imports for the current user.
    /// </summary>
    [HttpGet("completed/count")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<int>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<int>>> GetCompletedCount(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        var count = await _mediator.Send(new GetImportHistoryCountQuery(userId, DocumentProcessingStatus.Completed), cancellationToken);
        return Ok(count);
    }

    /// <summary>
    /// Lists completed imports for the current user.
    /// </summary>
    [HttpGet("completed")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ImportHistoryListDto>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<ImportHistoryListDto>>> GetCompleted([FromQuery] ImportHistoryListRequestDto requestDto, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        var dto = await _mediator.Send(new GetImportHistoryListQuery(requestDto.Page, requestDto.PageSize, userId, DocumentProcessingStatus.Completed), cancellationToken);
        return Ok(dto);
    }

    /// <summary>
    /// Counts failed imports for the current user.
    /// </summary>
    [HttpGet("failed/count")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<int>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<int>>> GetFailedCount(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        var count = await _mediator.Send(new GetImportHistoryCountQuery(userId, DocumentProcessingStatus.Failed), cancellationToken);
        return Ok(count);
    }

    /// <summary>
    /// Lists failed imports for the current user.
    /// </summary>
    [HttpGet("failed")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ImportHistoryListDto>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<ImportHistoryListDto>>> GetFailed([FromQuery] ImportHistoryListRequestDto requestDto, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        var dto = await _mediator.Send(new GetImportHistoryListQuery(requestDto.Page, requestDto.PageSize, userId, DocumentProcessingStatus.Failed), cancellationToken);
        return Ok(dto);
    }
}
