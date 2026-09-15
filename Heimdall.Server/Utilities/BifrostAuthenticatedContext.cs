using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Heimdall.Server
{
    /// <summary>
    /// Context supplied to a Bifrost authenticated-connection handler or inline callback.
    /// </summary>
    public sealed record BifrostAuthenticatedContext(
        HttpContext HttpContext,
        BifrostConnectionInfo Connection,
        IBifrostConnectionStore Connections,
        IServiceProvider Services)
    {
        /// <summary>
        /// Gets the authenticated principal for the SSE request.
        /// </summary>
        public ClaimsPrincipal User => HttpContext.User;

        /// <summary>
        /// Gets the cancellation token for the SSE request.
        /// </summary>
        public CancellationToken RequestAborted => HttpContext.RequestAborted;

        /// <summary>
        /// Sets application metadata on this connection.
        /// </summary>
        public bool TrySetMetadata(string key, string value)
            => Connections.TrySetMetadata(Connection.ConnectionId, key, value);

        /// <summary>
        /// Removes application metadata from this connection.
        /// </summary>
        public bool TryRemoveMetadata(string key)
            => Connections.TryRemoveMetadata(Connection.ConnectionId, key);
    }
}
