using System.Collections.Generic;
using System.Threading;
using ArquivoMate2.Application.Commands.Sharing;
using ArquivoMate2.Application.Queries.Sharing;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Shared.Models.Sharing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArquivoMate2.API.Controllers;

[ApiController]
[Authorize]
[Route("api/documents/{documentId:guid}/shares")]
public class DocumentSharesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public DocumentSharesController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<DocumentShareDto>))]
    public async Task<IActionResult> List(Guid documentId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetDocumentSharesQuery(documentId, _currentUserService.UserId), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(DocumentShareDto))]
    public async Task<IActionResult> Create(Guid documentId, [FromBody] CreateDocumentShareRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest();
        }

        var share = await _mediator.Send(new CreateDocumentShareCommand(documentId, _currentUserService.UserId, request.Target), cancellationToken);
        return CreatedAtAction(nameof(List), new { documentId }, share);
    }

    [HttpDelete("{shareId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid documentId, Guid shareId, CancellationToken cancellationToken)
    {
        var success = await _mediator.Send(new DeleteDocumentShareCommand(documentId, _currentUserService.UserId, shareId), cancellationToken);
        if (!success)
        {
            return NotFound();
        }

        return NoContent();
    }
}
