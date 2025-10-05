using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Commands.Collections;
using ArquivoMate2.Application.Queries.Collections;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Shared.Models.Collections;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArquivoMate2.API.Controllers;

[ApiController]
[Authorize]
[Route("api/collections")] 
public class CollectionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public CollectionsController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<CollectionDto>))]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var userId = _currentUserService.UserId;
        var result = await _mediator.Send(new ListCollectionsQuery(userId), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CollectionDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var userId = _currentUserService.UserId;
        var result = await _mediator.Send(new GetCollectionQuery(id, userId), ct);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(CollectionDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCollectionRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name)) return BadRequest();
        try
        {
            var userId = _currentUserService.UserId;
            var result = await _mediator.Send(new CreateCollectionCommand(userId, request.Name), ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CollectionDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCollectionRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name)) return BadRequest();
        try
        {
            var userId = _currentUserService.UserId;
            var result = await _mediator.Send(new UpdateCollectionCommand(id, userId, request.Name), ct);
            if (result is null) return NotFound();
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = _currentUserService.UserId;
        var success = await _mediator.Send(new DeleteCollectionCommand(id, userId), ct);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpPost("{id:guid}/assign")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AssignResultDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignDocumentsRequest request, CancellationToken ct)
    {
        if (request is null || request.DocumentIds is null) return BadRequest();
        if (request.CollectionId != Guid.Empty && request.CollectionId != id) return BadRequest(new { error = "CollectionId mismatch." });
        var userId = _currentUserService.UserId;
        try
        {
            var created = await _mediator.Send(new AssignDocumentsToCollectionCommand(id, userId, request.DocumentIds), ct);
            return Ok(new AssignResultDto { CreatedCount = created });
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id:guid}/documents/{documentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(Guid id, Guid documentId, CancellationToken ct)
    {
        var userId = _currentUserService.UserId;
        var success = await _mediator.Send(new RemoveDocumentFromCollectionCommand(id, documentId, userId), ct);
        if (!success) return NotFound();
        return NoContent();
    }
}
