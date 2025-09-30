using System.Collections.Generic;
using ArquivoMate2.Shared.Models.Users;
using MediatR;

namespace ArquivoMate2.Application.Queries.Users;

/// <summary>
/// Query to list all other users except the current one.
/// </summary>
/// <param name="CurrentUserId">Id of the requesting user.</param>
public sealed record ListOtherUsersQuery(string CurrentUserId) : IRequest<IReadOnlyCollection<UserDto>>;
