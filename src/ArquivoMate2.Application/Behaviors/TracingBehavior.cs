using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Interfaces;
using MediatR;

namespace ArquivoMate2.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that starts an Activity for each request so handlers are traced
/// centrally. Sets common tags (request type, user id) and records exceptions.
/// </summary>
public class TracingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly ActivitySource s_activity = new("ArquivoMate2.MediatRPipeline", "1.0");
    private readonly ICurrentUserService? _currentUserService;

    public TracingBehavior(ICurrentUserService? currentUserService = null)
    {
        _currentUserService = currentUserService;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // Start an activity for the MediatR request. If no listener is registered StartActivity returns null.
        using var activity = s_activity.StartActivity($"MediatR.{typeof(TRequest).Name}", ActivityKind.Internal);

        activity?.SetTag("mediatr.request", typeof(TRequest).FullName);

        if (_currentUserService != null)
        {
            try
            {
                var uid = _currentUserService.UserId;
                if (!string.IsNullOrEmpty(uid)) activity?.SetTag("user.id", uid);
            }
            catch
            {
                // Ignore - current user may not be available during some background operations
            }
        }

        // Optionally expose some known request properties for common request types
        // (avoid reflection for performance in hot paths; extend here if desired)

        try
        {
            var result = await next().ConfigureAwait(false);
            activity?.SetTag("mediatr.status", "OK");
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetTag("mediatr.status", "ERROR");
            activity?.SetTag("error", true);
            activity?.SetTag("error.message", ex.Message);
            // Record exception details as an ActivityEvent for compatibility
            var tags = new ActivityTagsCollection
            {
                { "exception.type", ex.GetType().FullName ?? string.Empty },
                { "exception.message", ex.Message }
            };
            activity?.AddEvent(new ActivityEvent("exception", default, tags));
            throw;
        }
    }
}
