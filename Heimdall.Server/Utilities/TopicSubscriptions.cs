using Heimdall.Server.Utilities;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Heimdall.Server
{
    internal sealed class TopicSubscriptions
    {
        private readonly ConcurrentDictionary<Guid, Subscriber> _subs = new();

        public BifrostSubscription Add(string topic, int perSubscriberBuffer, Action onEmpty)
        {
            var id = Guid.NewGuid();

            var channel = Channel.CreateBounded<BifrostMessage>(new BoundedChannelOptions(perSubscriberBuffer)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            }, dropped => HeimdallTelemetry.RecordBifrostDropped(dropped.EventName, "buffer_overflow"));

            var subscriber = new Subscriber(channel);
            _subs[id] = subscriber;
            HeimdallTelemetry.SubscriberOpened();

            void Unsubscribe()
            {
                if (_subs.TryRemove(id, out var removed))
                {
                    removed.Channel.Writer.TryComplete();
                    HeimdallTelemetry.SubscriberClosed();
                }

                if (_subs.IsEmpty)
                    onEmpty();
            }

            return new BifrostSubscription(
                id,
                channel.Reader,
                Unsubscribe,
                subscriber.DisconnectRequested,
                subscriber.RequestDisconnect);
        }

        public void Publish(BifrostMessage message)
        {
            foreach (var kv in _subs)
            {
                var subscriber = kv.Value;
                if (subscriber.IsDisconnectRequested)
                    continue;

                if (subscriber.Channel.Writer.TryWrite(message))
                {
                    HeimdallTelemetry.RecordBifrostDelivered(message.EventName);
                }
                else
                {
                    HeimdallTelemetry.RecordBifrostDropped(message.EventName, "subscriber_unavailable");
                }
            }
        }

        public int Disconnect(string reason)
        {
            var count = 0;

            foreach (var subscriber in _subs.Values)
            {
                if (subscriber.RequestDisconnect(reason))
                    count++;
            }

            return count;
        }

        public bool IsEmpty => _subs.IsEmpty;

        private sealed class Subscriber(Channel<BifrostMessage> channel)
        {
            private readonly TaskCompletionSource<string> _disconnect = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            private int _disconnectRequested;

            public Channel<BifrostMessage> Channel { get; } = channel;

            public Task<string> DisconnectRequested => _disconnect.Task;

            public bool IsDisconnectRequested
                => Volatile.Read(ref _disconnectRequested) != 0;

            public bool RequestDisconnect(string reason)
            {
                if (Interlocked.CompareExchange(ref _disconnectRequested, 1, 0) != 0)
                    return false;

                _disconnect.TrySetResult(reason);
                return true;
            }
        }
    }
}
