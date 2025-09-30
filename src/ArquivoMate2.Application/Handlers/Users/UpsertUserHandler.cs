using ArquivoMate2.Application.Commands.Users;
using ArquivoMate2.Domain.Users;
using ArquivoMate2.Shared.Models.Users;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Users;

public class UpsertUserHandler : IRequestHandler<UpsertUserCommand, UserDto>
{
    private readonly IDocumentSession _session;

    public UpsertUserHandler(IDocumentSession session)
    {
        _session = session;
    }

    public async Task<UserDto> Handle(UpsertUserCommand request, CancellationToken cancellationToken)
    {
        var trimmedName = string.IsNullOrWhiteSpace(request.Name)
            ? string.Empty
            : request.Name.Trim();

        var user = await _session.LoadAsync<UserProfile>(request.UserId, cancellationToken);

        if (user is null)
        {
            user = new UserProfile
            {
                Id = request.UserId,
                Name = trimmedName,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };
        }
        else
        {
            if (!string.Equals(user.Name, trimmedName, StringComparison.Ordinal))
            {
                user.Name = trimmedName;
            }

            user.LastLoginAt = DateTime.UtcNow;
        }

        _session.Store(user);
        await _session.SaveChangesAsync(cancellationToken);

        return new UserDto
        {
            Id = user.Id,
            Name = user.Name,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }
}
