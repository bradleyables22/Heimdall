using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace Heimdall.Server
{
    /// <summary>
    /// Default in-memory implementation of <see cref="IBifrostConnectionStore"/>.
    /// </summary>
    public sealed class BifrostConnectionStore : IBifrostConnectionStore
    {
        private readonly ConcurrentDictionary<Guid, Entry> _connections = new();

        /// <inheritdoc />
        public IReadOnlyList<BifrostConnectionInfo> Connections
            => GetConnections();

        /// <inheritdoc />
        public IReadOnlyList<BifrostConnectionInfo> GetConnections(BifrostConnectionSelector? selector = null)
            => _connections.Values
                .Select(static entry => entry.Snapshot())
                .Where(connection => selector is null || selector.Matches(connection))
                .ToArray();

        /// <inheritdoc />
        public bool TryGet(Guid connectionId, out BifrostConnectionInfo? connection)
        {
            if (_connections.TryGetValue(connectionId, out var entry))
            {
                connection = entry.Snapshot();
                return true;
            }

            connection = null;
            return false;
        }

        /// <inheritdoc />
        public bool TrySetMetadata(Guid connectionId, string key, string value)
        {
            ValidateMetadata(key, value);

            return _connections.TryGetValue(connectionId, out var entry) &&
                entry.TrySetMetadata(key, value);
        }

        /// <inheritdoc />
        public bool TryRemoveMetadata(Guid connectionId, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Metadata key is required.", nameof(key));

            return _connections.TryGetValue(connectionId, out var entry) &&
                entry.TryRemoveMetadata(key);
        }

        /// <inheritdoc />
        public int Disconnect(BifrostConnectionSelector selector, string? reason = null)
        {
            ArgumentNullException.ThrowIfNull(selector);
            selector.ValidateForDisconnect();
            reason = NormalizeReason(reason);

            var count = 0;
            foreach (var entry in _connections.Values)
            {
                if (selector.Matches(entry.Snapshot()) && entry.RequestDisconnect(reason))
                    count++;
            }

            return count;
        }

        internal void Register(BifrostConnectionInfo connection, Func<string, bool> requestDisconnect)
        {
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(requestDisconnect);

            if (!_connections.TryAdd(connection.ConnectionId, new Entry(connection, requestDisconnect)))
            {
                throw new InvalidOperationException(
                    $"A Bifrost connection with ID '{connection.ConnectionId}' is already registered.");
            }
        }

        internal BifrostConnectionInfo? Remove(Guid connectionId)
            => _connections.TryRemove(connectionId, out var entry)
                ? entry.Snapshot()
                : null;

        private static void ValidateMetadata(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Metadata key is required.", nameof(key));

            ArgumentNullException.ThrowIfNull(value);
        }

        private static string NormalizeReason(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return "server-disconnected";

            reason = reason.Trim();

            if (reason.Contains('\r') || reason.Contains('\n'))
                throw new ArgumentException(
                    "Subscriber disconnect reasons cannot contain newline characters.",
                    nameof(reason));

            return reason;
        }

        private sealed class Entry
        {
            private readonly object _gate = new();
            private readonly Func<string, bool> _requestDisconnect;
            private readonly Dictionary<string, string> _metadata;
            private BifrostConnectionInfo _connection;

            public Entry(BifrostConnectionInfo connection, Func<string, bool> requestDisconnect)
            {
                _connection = connection;
                _requestDisconnect = requestDisconnect;
                _metadata = new Dictionary<string, string>(connection.Metadata, StringComparer.Ordinal);
            }

            public BifrostConnectionInfo Snapshot()
            {
                lock (_gate)
                {
                    return _connection.WithMetadata(
                        new ReadOnlyDictionary<string, string>(
                            new Dictionary<string, string>(_metadata, StringComparer.Ordinal)));
                }
            }

            public bool TrySetMetadata(string key, string value)
            {
                lock (_gate)
                {
                    _metadata[key] = value;
                    return true;
                }
            }

            public bool TryRemoveMetadata(string key)
            {
                lock (_gate)
                {
                    return _metadata.Remove(key);
                }
            }

            public bool RequestDisconnect(string reason)
                => _requestDisconnect(reason);
        }
    }
}
