using System.Linq;
using ArquivoMate2.Domain.Users;
using Marten;
using Marten.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ArquivoMate2.API.Filters;

/// <summary>
/// Ensures that incoming requests contain a valid API key issued to a user profile.
/// </summary>
public class ApiKeyAuthorizationFilter : IAsyncActionFilter
{
    private readonly IQuerySession _querySession;
    private const string ApiKeyHeaderName = "X-Api-Key";

    public ApiKeyAuthorizationFilter(IQuerySession querySession)
    {
        _querySession = querySession;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        if (!httpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var headerValues))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var providedKey = headerValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedKey))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var user = await _querySession.Query<UserProfile>()
            .FirstOrDefaultAsync(x => x.ApiKey == providedKey, httpContext.RequestAborted);

        if (user is null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        httpContext.Items[nameof(UserProfile)] = user;
        await next();
    }
}
