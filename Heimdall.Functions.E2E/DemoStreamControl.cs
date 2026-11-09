using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Functions.E2E;

// Test-only transport interruption. Completes the actual HTTP stream without a terminal
// Bifrost disconnect directive, so the browser exercises its error/reconnect path.
public sealed class DemoStreamControl
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _streams = new();

    public async Task<IActionResult> RunAsync(HttpContext context, Func<Task<IActionResult>> stream)
    {
        var original = context.RequestAborted;
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(original);
        var id = Guid.NewGuid();
        _streams[id] = lifetime;
        context.RequestAborted = lifetime.Token;
        try { return await stream(); }
        finally
        {
            _streams.TryRemove(id, out _);
            context.RequestAborted = original;
        }
    }

    public void Interrupt()
    {
        foreach (var stream in _streams.Values)
        {
            try { stream.Cancel(); }
            catch (ObjectDisposedException) { }
        }
    }
}
