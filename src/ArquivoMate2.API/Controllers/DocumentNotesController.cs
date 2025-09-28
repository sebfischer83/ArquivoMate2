using ArquivoMate2.Application.Commands.Notes;
using ArquivoMate2.Application.Queries.Notes;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Shared.Models.Notes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArquivoMate2.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/documents/{documentId:guid}/notes")]
    public class DocumentNotesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ICurrentUserService _currentUserService;

        public DocumentNotesController(IMediator mediator, ICurrentUserService currentUserService)
        {
            _mediator = mediator;
            _currentUserService = currentUserService;
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(DocumentNoteDto))]
        public async Task<IActionResult> Create(Guid documentId, [FromBody] CreateDocumentNoteRequest request, CancellationToken ct)
        {
            var result = await _mediator.Send(new CreateDocumentNoteCommand(documentId, _currentUserService.UserId, request.Text), ct);
            return CreatedAtAction(nameof(List), new { documentId }, result);
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<DocumentNoteDto>))]
        public async Task<IActionResult> List(Guid documentId, [FromQuery] string? q, CancellationToken ct)
        {
            var result = await _mediator.Send(new GetDocumentNotesQuery(documentId, _currentUserService.UserId, q), ct);
            return Ok(result);
        }

        [HttpDelete("{noteId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Delete(Guid documentId, Guid noteId, CancellationToken ct)
        {
            var success = await _mediator.Send(new DeleteDocumentNoteCommand(documentId, noteId, _currentUserService.UserId), ct);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}
