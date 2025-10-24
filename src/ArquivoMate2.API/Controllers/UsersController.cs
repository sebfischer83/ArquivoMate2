using ArquivoMate2.Application.Commands.Users;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Queries.Users;
using ArquivoMate2.Shared.ApiModels;
using ArquivoMate2.Shared.Models.Users;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Org.BouncyCastle.Asn1.Ocsp;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;

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
    ///     Returns the current authenticated user profile without updating timestamps.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<CurrentUserDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CurrentUserDto>>> GetMe(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        var dto = await _mediator.Send(new GetCurrentUserQuery(userId), cancellationToken);
        if (dto is null)
            return NotFound();
        return Ok(new ApiResponse<CurrentUserDto>(dto));
    }

    /// <summary>
    ///     Synchronises the authenticated user with the application store.
    /// </summary>
    [HttpPost("login")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LoginToCookie([FromBody] UpsertUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpsertUserCommand(_currentUserService.UserId, request.Name), cancellationToken);
        var cookieIdentity = new ClaimsIdentity(User.Claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(cookieIdentity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = true,
            AllowRefresh = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        });

        return NoContent();
    }

    /// <summary>
    /// Returns all other users (for sharing dialogs etc.).
    /// </summary>
    [HttpGet("others")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<IEnumerable<UserDto>>))]
    public async Task<ActionResult<ApiResponse<IEnumerable<UserDto>>>> GetOthers(CancellationToken cancellationToken)
    {
        var currentId = _currentUserService.UserId;
        var users = await _mediator.Send(new ListOtherUsersQuery(currentId), cancellationToken);
        return Ok(users);
    }

    /// <summary>
    ///     Generates a new API key for the authenticated user and stores it with the profile.
    /// </summary>
    [HttpPost("api-key")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<UserApiKeyDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<UserApiKeyDto>>> GenerateApiKey(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(new GenerateUserApiKeyCommand(_currentUserService.UserId), cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
