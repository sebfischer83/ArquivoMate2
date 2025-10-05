using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Interfaces.Grouping;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Shared.Models.Grouping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArquivoMate2.API.Controllers;

[ApiController]
[Authorize]
[Route("api/documents/grouping")] 
public class DocumentGroupingController : ControllerBase
{
    private readonly IDocumentGroupingService _groupingService;
    private readonly ICurrentUserService _currentUserService;

    public DocumentGroupingController(IDocumentGroupingService groupingService, ICurrentUserService currentUserService)
    {
        _groupingService = groupingService;
        _currentUserService = currentUserService;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<DocumentGroupingNode>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Group([FromBody] DocumentGroupingRequest request, CancellationToken ct)
    {
        if (request == null) return BadRequest();
        try
        {
            var userId = _currentUserService.UserId;
            var nodes = await _groupingService.GroupAsync(userId, request, ct);
            return Ok(nodes);
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
}
