using ArquivoMate2.Application.Commands.Users;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Queries.Users;
using ArquivoMate2.Shared.Models.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;

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
    [OpenApiOperation(Summary = "Synchronise current user", Description = "Creates or updates the authenticated user within the application data store.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserDto))]
    public async Task<IActionResult> Upsert([FromBody] UpsertUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpsertUserCommand(_currentUserService.UserId, request.Name), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns all other users (for sharing dialogs etc.).
    /// </summary>
    [HttpGet("others")]
    [OpenApiOperation(Summary = "List other users", Description = "Returns all users except the current one for use in sharing dialogs.")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<UserDto>))]
    public async Task<IActionResult> GetOthers(CancellationToken cancellationToken)
    {
        var currentId = _currentUserService.UserId;
        var users = await _mediator.Send(new ListOtherUsersQuery(currentId), cancellationToken);
        return Ok(users);
    }
}
