using ArquivoMate2.Shared.Models.Users;
using MediatR;

namespace ArquivoMate2.Application.Commands.Users;

public record GenerateUserApiKeyCommand(string UserId) : IRequest<UserApiKeyDto>;
