using ArquivoMate2.Shared.Models.Users;
using MediatR;

namespace ArquivoMate2.Application.Commands.Users;

public record UpsertUserCommand(string UserId, string Name) : IRequest<UserDto>;
