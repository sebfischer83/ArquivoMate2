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

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<ShareGroupDto>))]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetShareGroupsQuery(_currentUserService.UserId), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
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

    [HttpGet("{groupId}")]
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

    [HttpPut("{groupId}")]
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

    [HttpDelete("{groupId}")]
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
