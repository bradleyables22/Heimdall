namespace Heimdall.Server
{
    /// <summary>
    /// Provides a thread-safe, current-application-instance view of active Bifrost SSE connections.
    /// </summary>
    /// <remarks>
    /// Connection records are created and removed by the Bifrost endpoint. Applications can inspect them, attach
    /// metadata, and request terminal disconnects. The store is local to the current process and is not a delivery or
    /// authorization system.
    /// </remarks>
    public interface IBifrostConnectionStore
    {
        /// <summary>
        /// Gets a snapshot of all active Bifrost connections.
        /// </summary>
        IReadOnlyList<BifrostConnectionInfo> Connections { get; }

        /// <summary>
        /// Gets a snapshot of active connections matching the optional selector.
        /// </summary>
        IReadOnlyList<BifrostConnectionInfo> GetConnections(BifrostConnectionSelector? selector = null);

        /// <summary>
        /// Gets one active connection by its server-generated ID.
        /// </summary>
        bool TryGet(Guid connectionId, out BifrostConnectionInfo? connection);

        /// <summary>
        /// Sets or replaces one application metadata value on an active connection.
        /// </summary>
        bool TrySetMetadata(Guid connectionId, string key, string value);

        /// <summary>
        /// Removes one application metadata value from an active connection.
        /// </summary>
        bool TryRemoveMetadata(Guid connectionId, string key);

        /// <summary>
        /// Requests a terminal disconnect for active connections matching the selector.
        /// </summary>
        /// <returns>The number of matching connections that accepted the request.</returns>
        int Disconnect(BifrostConnectionSelector selector, string? reason = null);
    }
}
