using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Queries.Users;
using ArquivoMate2.Domain.Users;
using ArquivoMate2.Shared.Models.Users;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Users;

/// <summary>
/// Handles retrieval of the current user profile without modifying last login timestamps.
/// </summary>
public sealed class GetCurrentUserHandler : IRequestHandler<GetCurrentUserQuery, CurrentUserDto?>
{
    private readonly IQuerySession _querySession;

    public GetCurrentUserHandler(IQuerySession querySession)
    {
        _querySession = querySession;
    }

    public async Task<CurrentUserDto?> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _querySession.LoadAsync<UserProfile>(request.UserId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        return new CurrentUserDto
        {
            Id = user.Id,
            Name = user.Name,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            ApiKey = user.ApiKey
        };
    }
}
