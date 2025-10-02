using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ArquivoMate2.Application.Commands.Sharing;
using ArquivoMate2.Application.Queries.Sharing;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Shared.Models.Sharing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;

namespace ArquivoMate2.API.Controllers;

[ApiController]
[Authorize]
[Route("api/share-groups")]
public class ShareGroupsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public ShareGroupsController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Lists all share groups owned by the current user.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token forwarded from the HTTP request.</param>
    [HttpGet]
    [OpenApiOperation(Summary = "List share groups", Description = "Returns the share groups that the current user has created.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<ShareGroupDto>))]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetShareGroupsQuery(_currentUserService.UserId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new share group with the specified members.
    /// </summary>
    /// <param name="request">Group definition including its name and members.</param>
    /// <param name="cancellationToken">Cancellation token forwarded from the HTTP request.</param>
    [HttpPost]
    [OpenApiOperation(Summary = "Create share group", Description = "Creates a share group and returns the stored representation.")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ShareGroupDto))]
    public async Task<IActionResult> Create([FromBody] CreateShareGroupRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest();
        }

        var group = await _mediator.Send(new CreateShareGroupCommand(_currentUserService.UserId, request.Name, request.MemberUserIds), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { groupId = group.Id }, group);
    }

    /// <summary>
    /// Retrieves a single share group by its identifier.
    /// </summary>
    /// <param name="groupId">Identifier of the group that should be retrieved.</param>
    /// <param name="cancellationToken">Cancellation token forwarded from the HTTP request.</param>
    [HttpGet("{groupId}")]
    [OpenApiOperation(Summary = "Get share group by id", Description = "Returns a single share group if it exists for the current user.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ShareGroupDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string groupId, CancellationToken cancellationToken)
    {
        var groups = await _mediator.Send(new GetShareGroupsQuery(_currentUserService.UserId), cancellationToken);
        var group = groups.FirstOrDefault(g => g.Id == groupId);
        if (group is null)
        {
            return NotFound();
        }

        return Ok(group);
    }

    /// <summary>
    /// Updates a share group with new metadata or members.
    /// </summary>
    /// <param name="groupId">Identifier of the group that should be updated.</param>
    /// <param name="request">Updated group details.</param>
    /// <param name="cancellationToken">Cancellation token forwarded from the HTTP request.</param>
    [HttpPut("{groupId}")]
    [OpenApiOperation(Summary = "Update share group", Description = "Updates the name or members of the specified share group.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ShareGroupDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string groupId, [FromBody] UpdateShareGroupRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest();
        }

        var result = await _mediator.Send(new UpdateShareGroupCommand(groupId, _currentUserService.UserId, request.Name, request.MemberUserIds), cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Deletes the specified share group.
    /// </summary>
    /// <param name="groupId">Identifier of the group that should be removed.</param>
    /// <param name="cancellationToken">Cancellation token forwarded from the HTTP request.</param>
    [HttpDelete("{groupId}")]
    [OpenApiOperation(Summary = "Delete share group", Description = "Removes the specified share group if it belongs to the current user.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string groupId, CancellationToken cancellationToken)
    {
        var success = await _mediator.Send(new DeleteShareGroupCommand(groupId, _currentUserService.UserId), cancellationToken);
        if (!success)
        {
            return NotFound();
        }

        return NoContent();
    }
}
