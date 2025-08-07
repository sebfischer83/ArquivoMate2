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
