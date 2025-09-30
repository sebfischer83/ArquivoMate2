using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Queries.Users;
using ArquivoMate2.Domain.Users;
using ArquivoMate2.Shared.Models.Users;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Users;

public class ListOtherUsersHandler : IRequestHandler<ListOtherUsersQuery, IReadOnlyCollection<UserDto>>
{
    private readonly IQuerySession _querySession;

    public ListOtherUsersHandler(IQuerySession querySession)
    {
        _querySession = querySession;
    }

    public async Task<IReadOnlyCollection<UserDto>> Handle(ListOtherUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _querySession.Query<UserProfile>()
            .Where(u => u.Id != request.CurrentUserId)
            .OrderBy(u => u.Name)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Name = u.Name,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt
            })
            .ToListAsync(cancellationToken);

        return users;
    }
}
