using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace BluffGame.Server.Auth;

/// <summary>
/// SignalR hub filter that enforces per-connection rate limiting.
/// Uses a sliding-window counter to cap invocations per time window.
/// </summary>
public class HubRateLimitFilter : IHubFilter
{
    private readonly ILogger<HubRateLimitFilter> _logger;

    // Config
    private const int MaxInvocationsPerWindow = 30;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    // ConnectionId → list of invocation timestamps
    private readonly ConcurrentDictionary<string, List<DateTime>> _tracker = new();

    public HubRateLimitFilter(ILogger<HubRateLimitFilter> logger)
    {
        _logger = logger;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var connectionId = invocationContext.Context.ConnectionId;
        var now = DateTime.UtcNow;
        var cutoff = now - Window;

        var timestamps = _tracker.GetOrAdd(connectionId, _ => new List<DateTime>());

        lock (timestamps)
        {
            // Remove entries outside the window
            timestamps.RemoveAll(t => t < cutoff);

            if (timestamps.Count >= MaxInvocationsPerWindow)
            {
                _logger.LogWarning(
                    "Rate limit exceeded for connection {ConnectionId} on method {Method}",
                    connectionId, invocationContext.HubMethodName);

                throw new HubException("Rate limit exceeded. Please slow down.");
            }

            timestamps.Add(now);
        }

        return await next(invocationContext);
    }

    public Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
        => next(context);

    public Task OnDisconnectedAsync(
        HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
    {
        // Clean up when connection closes
        _tracker.TryRemove(context.Context.ConnectionId, out _);
        return next(context, exception);
    }
}
