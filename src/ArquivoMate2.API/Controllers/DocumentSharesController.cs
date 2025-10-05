using System.Collections.Generic;
using System.Threading;
using ArquivoMate2.Application.Commands.Sharing;
using ArquivoMate2.Application.Queries.Sharing;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Shared.Models.Sharing;
using ArquivoMate2.Shared.ApiModels;
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

    /// <summary>
    /// Lists all shares that are configured for the specified document.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<IEnumerable<DocumentShareDto>>))]
    public async Task<ActionResult<ApiResponse<IEnumerable<DocumentShareDto>>>> List(Guid documentId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetDocumentSharesQuery(documentId, _currentUserService.UserId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new share for the selected document using the provided permissions.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<DocumentShareDto>))]
    public async Task<ActionResult<ApiResponse<DocumentShareDto>>> Create(Guid documentId, [FromBody] CreateDocumentShareRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest();
        }

        var share = await _mediator.Send(new CreateDocumentShareCommand(documentId, _currentUserService.UserId, request.Target, request.Permissions), cancellationToken);
        return CreatedAtAction(nameof(List), new { documentId }, share);
    }

    /// <summary>
    /// Removes a specific share from the selected document.
    /// </summary>
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
