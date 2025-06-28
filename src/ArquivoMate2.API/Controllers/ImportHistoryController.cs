using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Infrastructure.Persistance;
using ArquivoMate2.Shared.Models;
using Marten;
using Marten.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public ImportHistoryController(ICurrentUserService currentUserService, ILogger<ImportHistoryController> logger)
        {
            _currentUserService = currentUserService;
            _logger = logger;
        }

        [HttpGet()]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ImportHistoryListDto))]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get([FromQuery] ImportHistoryListRequestDto requestDto, CancellationToken cancellationToken, [FromServices] IQuerySession querySession, [FromServices] AutoMapper.IMapper mapper)
        {
            var userId = _currentUserService.UserId;

            var view = await querySession.Query<ImportHistoryView>()
                .Where(x => x.UserId == userId && !x.IsHidden)
                .ToPagedListAsync(requestDto.Page, requestDto.PageSize, cancellationToken);
            
            if (view is null || view.Count == 0)
                return NotFound();

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
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetInProgress([FromQuery] ImportHistoryListRequestDto requestDto, CancellationToken cancellationToken, [FromServices] IQuerySession querySession, [FromServices] AutoMapper.IMapper mapper)
        {
            var userId = _currentUserService.UserId;
            var view = await querySession.Query<ImportHistoryView>()
                .Where(x => x.UserId == userId && x.Status == DocumentProcessingStatus.InProgress && !x.IsHidden)
                .ToPagedListAsync(requestDto.Page, requestDto.PageSize, cancellationToken);
            if (view is null || view.Count == 0)
                return NotFound();

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
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPending([FromQuery] ImportHistoryListRequestDto requestDto, CancellationToken cancellationToken, [FromServices] IQuerySession querySession, [FromServices] AutoMapper.IMapper mapper)
        {
            var userId = _currentUserService.UserId;
            var view = await querySession.Query<ImportHistoryView>()
                .Where(x => x.UserId == userId && x.Status == DocumentProcessingStatus.Pending && !x.IsHidden)
                .ToPagedListAsync(requestDto.Page, requestDto.PageSize, cancellationToken);
            if (view is null || view.Count == 0)
                return NotFound();

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
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCompleted([FromQuery] ImportHistoryListRequestDto requestDto, CancellationToken cancellationToken, [FromServices] IQuerySession querySession, [FromServices] AutoMapper.IMapper mapper)
        {
            var userId = _currentUserService.UserId;
            var view = await querySession.Query<ImportHistoryView>()
                .Where(x => x.UserId == userId && x.Status == DocumentProcessingStatus.Completed && !x.IsHidden)
                .ToPagedListAsync(requestDto.Page, requestDto.PageSize, cancellationToken);
            if (view is null || view.Count == 0)
                return NotFound();

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
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetFailed([FromQuery] ImportHistoryListRequestDto requestDto, CancellationToken cancellationToken, [FromServices] IQuerySession querySession, [FromServices] AutoMapper.IMapper mapper)
        {
            var userId = _currentUserService.UserId;
            var view = await querySession.Query<ImportHistoryView>()
                .Where(x => x.UserId == userId && x.Status == DocumentProcessingStatus.Failed && !x.IsHidden)
                .ToPagedListAsync(requestDto.Page, requestDto.PageSize, cancellationToken);
            if (view is null || view.Count == 0)
                return NotFound();

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
