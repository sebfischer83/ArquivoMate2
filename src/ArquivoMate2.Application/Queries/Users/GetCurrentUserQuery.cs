using ArquivoMate2.Shared.Models.Users;
using MediatR;

namespace ArquivoMate2.Application.Queries.Users;

/// <summary>
/// Query to retrieve the current (authenticated) user profile without performing an upsert.
/// Returns <c>null</c> when the profile does not yet exist (e.g. user never called the login sync endpoint).
/// </summary>
/// <param name="UserId">The hashed identifier of the current user.</param>
public sealed record GetCurrentUserQuery(string UserId) : IRequest<CurrentUserDto?>;
