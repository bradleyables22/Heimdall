using System.Collections.ObjectModel;
using System.Security.Claims;

namespace Heimdall.Server
{
    /// <summary>
    /// Describes one authenticated Bifrost SSE connection on the current application instance.
    /// </summary>
    /// <remarks>
    /// The framework-owned connection ID, topic, subject, and connected timestamp are immutable. Applications can
    /// attach non-authoritative metadata through <see cref="IBifrostConnectionStore"/> for diagnostics, presence, or
    /// later connection selection. The metadata is an in-memory snapshot and is not a substitute for authorization.
    /// </remarks>
    public sealed class BifrostConnectionInfo
    {
        internal BifrostConnectionInfo(
            Guid connectionId,
            string topic,
            string? subject,
            DateTimeOffset connectedAt,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            ConnectionId = connectionId;
            Topic = topic;
            Subject = subject;
            ConnectedAt = connectedAt;
            Metadata = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(
                    metadata ?? new Dictionary<string, string>(),
                    StringComparer.Ordinal));
        }

        /// <summary>
        /// Gets the server-generated identifier for the native SSE connection.
        /// </summary>
        public Guid ConnectionId { get; }

        /// <summary>
        /// Gets the topic subscribed to by the connection.
        /// </summary>
        public string Topic { get; }

        /// <summary>
        /// Gets the stable authenticated subject, when the principal supplied a name-identifier or <c>sub</c> claim.
        /// </summary>
        public string? Subject { get; }

        /// <summary>
        /// Gets the UTC time at which the server accepted the SSE connection.
        /// </summary>
        public DateTimeOffset ConnectedAt { get; }

        /// <summary>
        /// Gets the application metadata currently associated with the connection.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; }

        internal static BifrostConnectionInfo Create(
            Guid connectionId,
            string topic,
            ClaimsPrincipal user)
            => new(
                connectionId,
                topic,
                GetSubject(user),
                DateTimeOffset.UtcNow);

        internal BifrostConnectionInfo WithMetadata(IReadOnlyDictionary<string, string> metadata)
            => new(ConnectionId, Topic, Subject, ConnectedAt, metadata);

        private static string? GetSubject(ClaimsPrincipal user)
        {
            var subject = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(subject))
                subject = user.FindFirst("sub")?.Value;

            return string.IsNullOrWhiteSpace(subject) ? null : subject;
        }
    }
}
