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
[Route("api/share-automation-rules")]
public class ShareAutomationRulesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public ShareAutomationRulesController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Lists automation rules that automatically share documents for the current user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<IEnumerable<ShareAutomationRuleDto>>))]
    public async Task<ActionResult<ApiResponse<IEnumerable<ShareAutomationRuleDto>>>> List(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetShareAutomationRulesQuery(_currentUserService.UserId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new automation rule that shares matching documents automatically.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<ShareAutomationRuleDto>))]
    public async Task<ActionResult<ApiResponse<ShareAutomationRuleDto>>> Create([FromBody] CreateShareAutomationRuleRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest();
        }

        var rule = await _mediator.Send(new CreateShareAutomationRuleCommand(_currentUserService.UserId, request.Target, request.Scope, request.Permissions), cancellationToken);
        return CreatedAtAction(nameof(List), null, rule);
    }

    /// <summary>
    /// Deletes an existing share automation rule.
    /// </summary>
    [HttpDelete("{ruleId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string ruleId, CancellationToken cancellationToken)
    {
        var success = await _mediator.Send(new DeleteShareAutomationRuleCommand(ruleId, _currentUserService.UserId), cancellationToken);
        if (!success)
        {
            return NotFound();
        }

        return NoContent();
    }
}
