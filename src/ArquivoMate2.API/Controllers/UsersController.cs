using ArquivoMate2.Application.Commands.Users;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Shared.Models.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArquivoMate2.API.Controllers;

[ApiController]
[Authorize]
[Route("api/users")] 
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public UsersController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    /// <summary>
    ///     Synchronises the authenticated user with the application store.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserDto))]
    public async Task<IActionResult> Upsert([FromBody] UpsertUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpsertUserCommand(_currentUserService.UserId, request.Name), cancellationToken);
        return Ok(result);
    }
}
