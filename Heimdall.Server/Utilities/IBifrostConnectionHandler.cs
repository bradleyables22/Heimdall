namespace Heimdall.Server
{
    /// <summary>
    /// Observes the authenticated and disconnected lifecycle of Bifrost SSE connections.
    /// </summary>
    /// <remarks>
    /// Handlers are resolved in a short-lived callback scope, so they can inject scoped services such as a database
    /// context without keeping those services alive for the duration of the SSE stream. This is an observation and
    /// enrichment hook; use <see cref="IBifrostTopicAuthHandler"/> to allow or deny a topic.
    /// </remarks>
    public interface IBifrostConnectionHandler
    {
        /// <summary>
        /// Runs after the subscribe token has been validated and the connection has been registered.
        /// </summary>
        ValueTask OnBifrostAuthenticatedAsync(BifrostAuthenticatedContext context)
            => ValueTask.CompletedTask;

        /// <summary>
        /// Runs when a registered connection is removed from the store.
        /// </summary>
        ValueTask OnBifrostDisconnectedAsync(BifrostDisconnectedContext context)
            => ValueTask.CompletedTask;
    }
}
