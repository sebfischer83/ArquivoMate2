using System.Collections.Generic;
using System.Security.Cryptography;
using ArquivoMate2.Application.Commands.Users;
using ArquivoMate2.Domain.Users;
using ArquivoMate2.Shared.Models.Users;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.Users;

public class GenerateUserApiKeyHandler : IRequestHandler<GenerateUserApiKeyCommand, UserApiKeyDto>
{
    private readonly IDocumentSession _session;

    public GenerateUserApiKeyHandler(IDocumentSession session)
    {
        _session = session;
    }

    public async Task<UserApiKeyDto> Handle(GenerateUserApiKeyCommand request, CancellationToken cancellationToken)
    {
        var user = await _session.LoadAsync<UserProfile>(request.UserId, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException("User profile not found");
        }

        var apiKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        user.ApiKey = apiKey;
        _session.Store(user);
        await _session.SaveChangesAsync(cancellationToken);

        return new UserApiKeyDto
        {
            ApiKey = apiKey
        };
    }
}
