namespace Heimdall.Server
{
    /// <summary>
    /// Selects Bifrost connections using exact framework-owned fields and application metadata.
    /// </summary>
    /// <remarks>
    /// All populated fields must match. Topic comparisons are case-insensitive; subject, connection ID, and metadata
    /// comparisons are exact. An empty selector can inspect all connections but cannot be used to disconnect them.
    /// </remarks>
    public sealed record BifrostConnectionSelector
    {
        /// <summary>
        /// Gets or initializes an exact topic filter.
        /// </summary>
        public string? Topic { get; init; }

        /// <summary>
        /// Gets or initializes an exact authenticated subject filter.
        /// </summary>
        public string? Subject { get; init; }

        /// <summary>
        /// Gets or initializes an exact server-generated connection ID filter.
        /// </summary>
        public Guid? ConnectionId { get; init; }

        /// <summary>
        /// Gets or initializes metadata entries that must all match.
        /// </summary>
        public IReadOnlyDictionary<string, string>? Metadata { get; init; }

        /// <summary>
        /// Creates a selector for all connections belonging to a subject.
        /// </summary>
        public static BifrostConnectionSelector ForSubject(string subject)
        {
            if (string.IsNullOrWhiteSpace(subject))
                throw new ArgumentException("Subject is required.", nameof(subject));

            return new() { Subject = subject };
        }

        /// <summary>
        /// Creates a selector for one topic.
        /// </summary>
        public static BifrostConnectionSelector ForTopic(string topic)
        {
            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("Topic is required.", nameof(topic));

            return new() { Topic = topic };
        }

        /// <summary>
        /// Creates a selector for one server-side connection.
        /// </summary>
        public static BifrostConnectionSelector ForConnection(Guid connectionId)
            => new() { ConnectionId = connectionId };

        internal bool Matches(BifrostConnectionInfo connection)
        {
            if (Topic is not null && !string.Equals(Topic, connection.Topic, StringComparison.OrdinalIgnoreCase))
                return false;

            if (Subject is not null && !string.Equals(Subject, connection.Subject, StringComparison.Ordinal))
                return false;

            if (ConnectionId is not null && ConnectionId != connection.ConnectionId)
                return false;

            if (Metadata is not null)
            {
                foreach (var pair in Metadata)
                {
                    if (!connection.Metadata.TryGetValue(pair.Key, out var value) ||
                        !string.Equals(value, pair.Value, StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        internal void ValidateForDisconnect()
        {
            if (string.IsNullOrWhiteSpace(Topic) &&
                string.IsNullOrWhiteSpace(Subject) &&
                ConnectionId is null &&
                (Metadata is null || Metadata.Count == 0))
            {
                throw new ArgumentException(
                    "A Bifrost connection selector must specify a topic, subject, connection ID, or metadata.",
                    nameof(BifrostConnectionSelector));
            }

            if (Topic is not null && string.IsNullOrWhiteSpace(Topic))
                throw new ArgumentException("Topic cannot be empty or whitespace.", nameof(Topic));

            if (Subject is not null && string.IsNullOrWhiteSpace(Subject))
                throw new ArgumentException("Subject cannot be empty or whitespace.", nameof(Subject));

            if (Metadata is null)
                return;

            foreach (var pair in Metadata)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    throw new ArgumentException("Metadata keys cannot be empty or whitespace.", nameof(Metadata));

                if (pair.Value is null)
                    throw new ArgumentException("Metadata values cannot be null.", nameof(Metadata));
            }
        }
    }
}
