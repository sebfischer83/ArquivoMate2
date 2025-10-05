using ArquivoMate2.Application.Commands.Parties;
using ArquivoMate2.Application.Queries.Parties;
using ArquivoMate2.Shared.Models;
using ArquivoMate2.Shared.Models.Party;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ArquivoMate2.API.Controllers;

[ApiController]
[Authorize]
[Route("api/parties")]
public class PartiesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PartiesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<PartyDto>))]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var parties = await _mediator.Send(new ListPartiesQuery(), ct);
        return Ok(parties);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PartyDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var party = await _mediator.Send(new GetPartyQuery(id), ct);
        if (party is null) return NotFound();
        return Ok(party);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(PartyDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePartyRequest request, CancellationToken ct)
    {
        if (request is null) return BadRequest();

        var result = await _mediator.Send(new CreatePartyCommand(
            request.FirstName,
            request.LastName,
            request.CompanyName,
            request.Street,
            request.HouseNumber,
            request.PostalCode,
            request.City), ct);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PartyDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePartyRequest request, CancellationToken ct)
    {
        if (request is null || request.Id == Guid.Empty || request.Id != id)
        {
            return BadRequest();
        }

        var updated = await _mediator.Send(new UpdatePartyCommand(
            id,
            request.FirstName,
            request.LastName,
            request.CompanyName,
            request.Street,
            request.HouseNumber,
            request.PostalCode,
            request.City), ct);

        if (updated is null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var success = await _mediator.Send(new DeletePartyCommand(id), ct);
        if (!success) return NotFound();
        return NoContent();
    }
}
