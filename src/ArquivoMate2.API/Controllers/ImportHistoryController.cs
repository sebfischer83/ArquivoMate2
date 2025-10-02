using ArquivoMate2.Application.Commands;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Infrastructure.Persistance;
using ArquivoMate2.Shared.Models;
using Marten;
using Marten.Pagination;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace ArquivoMate2.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/history")]
    public class ImportHistoryController : ControllerBase
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<ImportHistoryController> _logger;
        private readonly IStringLocalizer<ImportHistoryController> _stringLocalizer;

        public ImportHistoryController(ICurrentUserService currentUserService, ILogger<ImportHistoryController> logger, IStringLocalizer<ImportHistoryController> stringLocalizer)
        {
            _currentUserService = currentUserService;
            _logger = logger;
            _stringLocalizer = stringLocalizer;
        }

        /// <summary>
        /// Hides every import history entry that matches the supplied processing status for the current user.
        /// </summary>
        /// <param name="documentProcessingStatus">Status value whose entries should be removed from the history.</param>
        /// <param name="cancellationToken">Cancellation token forwarded from the HTTP request.</param>
        /// <param name="mediator">Mediator used to execute the hide command.</param>
        [HttpPost("hideByStatus")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> HideAllFromImportHistory(DocumentProcessingStatus documentProcessingStatus, CancellationToken cancellationToken, [FromServices] IMediator mediator)
        {
            var userId = _currentUserService.UserId;
            
            var success = await mediator.Send(new HideAllFromImportHistoryByStatusCommand(documentProcessingStatus, userId), cancellationToken);

            if (!success)
            {
                _logger.LogError("Failed to hide import history entries with status {Status} for user {UserId}", documentProcessingStatus, userId);
                return StatusCode(StatusCodes.Status500InternalServerError, _stringLocalizer.GetString("Failed to hide import history entries."));
            }

            return NoContent();
        }

        /// <summary>
        /// Retrieves a paged list of import history entries for the current user.
        /// </summary>
        /// <param name="requestDto">Paging information describing which page to load.</param>
        /// <param name="cancellationToken">Cancellation token forwarded from the HTTP request.</param>
        /// <param name="querySession">Query session used to read import history projections.</param>
        /// <param name="mapper">Mapper instance that transforms projections into DTOs.</param>
        [HttpGet()]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ImportHistoryListDto))]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Get([FromQuery] ImportHistoryListRequestDto requestDto, CancellationToken cancellationToken, [FromServices] IQuerySession querySession, [FromServices] AutoMapper.IMapper mapper)
        {
            var userId = _currentUserService.UserId;

            var view = await querySession.Query<ImportHistoryView>()
                .Where(x => x.UserId == userId && !x.IsHidden)
                .ToPagedListAsync(requestDto.Page, requestDto.PageSize, cancellationToken);
            
            if (view is null || view.Count == 0)
            {
                return Ok(new ImportHistoryListDto
                {
                    Items = Array.Empty<ImportHistoryListItemDto>(),
                    TotalCount = 0,
                    HasNextPage = false,
                    PageCount = 0,
                    HasPreviousPage = false,
                    IsLastPage = true,
                    IsFirstPage = true,
                    CurrentPage = requestDto.Page
                });
            }

            var items = mapper.Map<ImportHistoryListItemDto[]>(view);
            var result = new ImportHistoryListDto
            {
                Items = items,
                TotalCount = view.TotalItemCount,
                HasNextPage = view.HasNextPage,
                PageCount = view.PageCount,
                HasPreviousPage = view.HasPreviousPage,
                IsLastPage = view.IsLastPage,
                IsFirstPage = view.IsFirstPage,
                CurrentPage = requestDto.Page
            };
            return Ok(result);
        }

        /// <summary>
        /// Counts all imports that are currently being processed for the current user.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token forwarded from the HTTP request.</param>
        /// <param name="querySession">Query session used to count import history entries.</param>
        [HttpGet("inprogress/count")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(int))]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetInProgressCount(CancellationToken cancellationToken, [FromServices] IQuerySession querySession)
        {
            var userId = _currentUserService.UserId;
            var count = await querySession.Query<ImportHistoryView>()
                .Where(x => x.UserId == userId && x.Status == DocumentProcessingStatus.InProgress && !x.IsHidden)
                .CountAsync(cancellationToken);
            return Ok(count);
        }

        /// <summary>
        /// Lists imports that are currently in progress for the current user.
        /// </summary>
        /// <param name="requestDto">Paging information describing which page to load.</param>
        /// <param name="cancellationToken">Cancellation token forwarded from the HTTP request.</param>
        /// <param name="querySession">Query session used to read import history projections.</param>
        /// <param name="mapper">Mapper instance that transforms projections into DTOs.</param>
        [HttpGet("inprogress")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ImportHistoryListDto))]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetInProgress([FromQuery] ImportHistoryListRequestDto requestDto, CancellationToken cancellationToken, [FromServices] IQuerySession querySession, [FromServices] AutoMapper.IMapper mapper)
        {
            var userId = _currentUserService.UserId;
            var view = await querySession.Query<ImportHistoryView>()
                .Where(x => x.UserId == userId && x.Status == DocumentProcessingStatus.InProgress && !x.IsHidden)
                .ToPagedListAsync(requestDto.Page, requestDto.PageSize, cancellationToken);
            
            if (view is null || view.Count == 0)
            {
                return Ok(new ImportHistoryListDto
                {
                    Items = Array.Empty<ImportHistoryListItemDto>(),
                    TotalCount = 0,
                    HasNextPage = false,
                    PageCount = 0,
                    HasPreviousPage = false,
                    IsLastPage = true,
                    IsFirstPage = true,
                    CurrentPage = requestDto.Page
                });
            }

            var items = mapper.Map<ImportHistoryListItemDto[]>(view);
            var result = new ImportHistoryListDto
            {
                Items = items,
                TotalCount = view.TotalItemCount,
                HasNextPage = view.HasNextPage,
                PageCount = view.PageCount,
                HasPreviousPage = view.HasPreviousPage,
                IsLastPage = view.IsLastPage,
                IsFirstPage = view.IsFirstPage,
                CurrentPage = requestDto.Page
            };
            return Ok(result);
        }

        /// <summary>
        /// Counts pending imports that are queued for processing.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token forwarded from the HTTP request.</param>
        /// <param name="querySession">Query session used to count import history entries.</param>
        [HttpGet("pending/count")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(int))]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetPendingCount(CancellationToken cancellationToken, [FromServices] IQuerySession querySession)
        {
            var userId = _currentUserService.UserId;
            var count = await querySession.Query<ImportHistoryView>()
                .Where(x => x.UserId == userId && x.Status == DocumentProcessingStatus.Pending && !x.IsHidden)
                .CountAsync(cancellationToken);
            return Ok(count);
        }

        /// <summary>
        /// Lists pending imports that are waiting to be processed.
        /// </summary>
        /// <param name="requestDto">Paging information describing which page to load.</param>
        /// <param name="cancellationToken">Cancellation token forwarded from the HTTP request.</param>
        /// <param name="querySession">Query session used to read import history projections.</param>
        /// <param name="mapper">Mapper instance that transforms projections into DTOs.</param>
        [HttpGet("pending")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ImportHistoryListDto))]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetPending([FromQuery] ImportHistoryListRequestDto requestDto, CancellationToken cancellationToken, [FromServices] IQuerySession querySession, [FromServices] AutoMapper.IMapper mapper)
        {
            var userId = _currentUserService.UserId;
            var view = await querySession.Query<ImportHistoryView>()
                .Where(x => x.UserId == userId && x.Status == DocumentProcessingStatus.Pending && !x.IsHidden)
                .ToPagedListAsync(requestDto.Page, requestDto.PageSize, cancellationToken);
            
            if (view is null || view.Count == 0)
            {
                return Ok(new ImportHistoryListDto
                {
                    Items = Array.Empty<ImportHistoryListItemDto>(),
                    TotalCount = 0,
                    HasNextPage = false,
                    PageCount = 0,
                    HasPreviousPage = false,
                    IsLastPage = true,
                    IsFirstPage = true,
                    CurrentPage = requestDto.Page
                });
            }

            var items = mapper.Map<ImportHistoryListItemDto[]>(view);
            var result = new ImportHistoryListDto
            {
                Items = items,
                TotalCount = view.TotalItemCount,
                HasNextPage = view.HasNextPage,
                PageCount = view.PageCount,
                HasPreviousPage = view.HasPreviousPage,
                IsLastPage = view.IsLastPage,
                IsFirstPage = view.IsFirstPage,
                CurrentPage = requestDto.Page
            };
            return Ok(result);
        }

        /// <summary>
        /// Counts completed imports for the current user.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token forwarded from the HTTP request.</param>
        /// <param name="querySession">Query session used to count import history entries.</param>
        [HttpGet("completed/count")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(int))]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetCompletedCount(CancellationToken cancellationToken, [FromServices] IQuerySession querySession)
        {
            var userId = _currentUserService.UserId;
            var count = await querySession.Query<ImportHistoryView>()
                .Where(x => x.UserId == userId && x.Status == DocumentProcessingStatus.Completed && !x.IsHidden)
                .CountAsync(cancellationToken);
            return Ok(count);
        }

        /// <summary>
        /// Lists completed imports for the current user.
        /// </summary>
        /// <param name="requestDto">Paging information describing which page to load.</param>
        /// <param name="cancellationToken">Cancellation token forwarded from the HTTP request.</param>
        /// <param name="querySession">Query session used to read import history projections.</param>
        /// <param name="mapper">Mapper instance that transforms projections into DTOs.</param>
        [HttpGet("completed")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ImportHistoryListDto))]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetCompleted([FromQuery] ImportHistoryListRequestDto requestDto, CancellationToken cancellationToken, [FromServices] IQuerySession querySession, [FromServices] AutoMapper.IMapper mapper)
        {
            var userId = _currentUserService.UserId;
            var view = await querySession.Query<ImportHistoryView>()
                .Where(x => x.UserId == userId && x.Status == DocumentProcessingStatus.Completed && !x.IsHidden)
                .ToPagedListAsync(requestDto.Page, requestDto.PageSize, cancellationToken);
            
            if (view is null || view.Count == 0)
            {
                return Ok(new ImportHistoryListDto
                {
                    Items = Array.Empty<ImportHistoryListItemDto>(),
                    TotalCount = 0,
                    HasNextPage = false,
                    PageCount = 0,
                    HasPreviousPage = false,
                    IsLastPage = true,
                    IsFirstPage = true,
                    CurrentPage = requestDto.Page
                });
            }

            var items = mapper.Map<ImportHistoryListItemDto[]>(view);
            var result = new ImportHistoryListDto
            {
                Items = items,
                TotalCount = view.TotalItemCount,
                HasNextPage = view.HasNextPage,
                PageCount = view.PageCount,
                HasPreviousPage = view.HasPreviousPage,
                IsLastPage = view.IsLastPage,
                IsFirstPage = view.IsFirstPage,
                CurrentPage = requestDto.Page
            };
            return Ok(result);
        }

        /// <summary>
        /// Counts failed imports for the current user.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token forwarded from the HTTP request.</param>
        /// <param name="querySession">Query session used to count import history entries.</param>
        [HttpGet("failed/count")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(int))]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetFailedCount(CancellationToken cancellationToken, [FromServices] IQuerySession querySession)
        {
            var userId = _currentUserService.UserId;
            var count = await querySession.Query<ImportHistoryView>()
                .Where(x => x.UserId == userId && x.Status == DocumentProcessingStatus.Failed && !x.IsHidden)
                .CountAsync(cancellationToken);
            return Ok(count);
        }

        /// <summary>
        /// Lists failed imports for the current user.
        /// </summary>
        /// <param name="requestDto">Paging information describing which page to load.</param>
        /// <param name="cancellationToken">Cancellation token forwarded from the HTTP request.</param>
        /// <param name="querySession">Query session used to read import history projections.</param>
        /// <param name="mapper">Mapper instance that transforms projections into DTOs.</param>
        [HttpGet("failed")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ImportHistoryListDto))]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetFailed([FromQuery] ImportHistoryListRequestDto requestDto, CancellationToken cancellationToken, [FromServices] IQuerySession querySession, [FromServices] AutoMapper.IMapper mapper)
        {
            var userId = _currentUserService.UserId;
            var view = await querySession.Query<ImportHistoryView>()
                .Where(x => x.UserId == userId && x.Status == DocumentProcessingStatus.Failed && !x.IsHidden)
                .ToPagedListAsync(requestDto.Page, requestDto.PageSize, cancellationToken);
            
            if (view is null || view.Count == 0)
            {
                return Ok(new ImportHistoryListDto
                {
                    Items = Array.Empty<ImportHistoryListItemDto>(),
                    TotalCount = 0,
                    HasNextPage = false,
                    PageCount = 0,
                    HasPreviousPage = false,
                    IsLastPage = true,
                    IsFirstPage = true,
                    CurrentPage = requestDto.Page
                });
            }

            var items = mapper.Map<ImportHistoryListItemDto[]>(view);
            var result = new ImportHistoryListDto
            {
                Items = items,
                TotalCount = view.TotalItemCount,
                HasNextPage = view.HasNextPage,
                PageCount = view.PageCount,
                HasPreviousPage = view.HasPreviousPage,
                IsLastPage = view.IsLastPage,
                IsFirstPage = view.IsFirstPage,
                CurrentPage = requestDto.Page
            };
            return Ok(result);
        }
    }
}
