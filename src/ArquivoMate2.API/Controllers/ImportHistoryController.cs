using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Infrastructure.Persistance;
using ArquivoMate2.Shared.Models;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArquivoMate2.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/history")]
    public class ImportHistoryController : ControllerBase
    {
        private readonly ICurrentUserService _currentUserService;

        public ImportHistoryController(ICurrentUserService currentUserService)
        {
            _currentUserService = currentUserService;
        }

        [HttpGet()]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(CancellationToken cancellationToken, [FromServices] IQuerySession querySession)
        {
            var userId = _currentUserService.UserId;
            //var view = await querySession.Query<Infrastructure.Persistance.DocumentView>().Where(d => d.UserId == userId && d.Processed == true).
            //    ToPagedListAsync(requestDto.Page, requestDto.PageSize);
            //if (view is null)
            //    return NotFound();
            //if (view.Count == 0)
            //    return NotFound();

            var view = querySession.Query<ImportHistoryView>().ToList();


            //DocumentListDto documentListDto = new();
            //var documents = _mapper.Map<DocumentListItemDto[]>(view);
            //documentListDto.Documents = documents;

            //documentListDto.TotalCount = view.TotalItemCount;
            //documentListDto.HasNextPage = view.HasNextPage;
            //documentListDto.PageCount = view.PageCount;
            //documentListDto.HasPreviousPage = view.HasPreviousPage;
            //documentListDto.IsLastPage = view.IsLastPage;
            //documentListDto.IsFirstPage = view.IsFirstPage;
            //documentListDto.CurrentPage = requestDto.Page;

            return Ok(view);

        }
    }
}
