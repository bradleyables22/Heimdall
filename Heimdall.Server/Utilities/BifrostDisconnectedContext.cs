using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Heimdall.Server
{
    /// <summary>
    /// Context supplied to a Bifrost disconnected-connection handler or inline callback.
    /// </summary>
    public sealed record BifrostDisconnectedContext(
        HttpContext HttpContext,
        BifrostConnectionInfo Connection,
        string Reason,
        IServiceProvider Services)
    {
        /// <summary>
        /// Gets the principal associated with the disconnected SSE request.
        /// </summary>
        public ClaimsPrincipal User => HttpContext.User;

        /// <summary>
        /// Gets the cancellation token for the SSE request.
        /// </summary>
        public CancellationToken RequestAborted => HttpContext.RequestAborted;
    }
}
