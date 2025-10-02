using ArquivoMate2.Application.Commands.Notes;
using ArquivoMate2.Application.Queries.Notes;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Shared.Models.Notes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;

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

        /// <summary>
        /// Creates a note that is attached to the specified document for the current user.
        /// </summary>
        /// <param name="documentId">Identifier of the document that owns the note.</param>
        /// <param name="request">Note payload containing the text that should be stored.</param>
        /// <param name="ct">Cancellation token forwarded from the HTTP request.</param>
        [HttpPost]
        [OpenApiOperation(Summary = "Create a document note", Description = "Creates a new note for the selected document and returns the persisted representation.")]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(DocumentNoteDto))]
        public async Task<IActionResult> Create(Guid documentId, [FromBody] CreateDocumentNoteRequest request, CancellationToken ct)
        {
            var result = await _mediator.Send(new CreateDocumentNoteCommand(documentId, _currentUserService.UserId, request.Text), ct);
            return CreatedAtAction(nameof(List), new { documentId }, result);
        }

        /// <summary>
        /// Lists notes for the specified document filtered by an optional search term.
        /// </summary>
        /// <param name="documentId">Identifier of the document whose notes should be listed.</param>
        /// <param name="q">Optional free text filter that narrows the result.</param>
        /// <param name="ct">Cancellation token forwarded from the HTTP request.</param>
        [HttpGet]
        [OpenApiOperation(Summary = "List document notes", Description = "Retrieves all notes created for the specified document. A search term can be supplied to filter the results.")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<DocumentNoteDto>))]
        public async Task<IActionResult> List(Guid documentId, [FromQuery] string? q, CancellationToken ct)
        {
            var result = await _mediator.Send(new GetDocumentNotesQuery(documentId, _currentUserService.UserId, q), ct);
            return Ok(result);
        }

        /// <summary>
        /// Removes a single document note that belongs to the current user.
        /// </summary>
        /// <param name="documentId">Identifier of the document that owns the note.</param>
        /// <param name="noteId">Identifier of the note to remove.</param>
        /// <param name="ct">Cancellation token forwarded from the HTTP request.</param>
        [HttpDelete("{noteId:guid}")]
        [OpenApiOperation(Summary = "Delete a document note", Description = "Deletes the specified note if it belongs to the document and is owned by the current user.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Delete(Guid documentId, Guid noteId, CancellationToken ct)
        {
            var success = await _mediator.Send(new DeleteDocumentNoteCommand(documentId, noteId, _currentUserService.UserId), ct);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}
