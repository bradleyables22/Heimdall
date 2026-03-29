using Heimdall.Server.Utilities;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Heimdall.Server
{
    internal sealed class TopicSubscriptions
    {
        private readonly ConcurrentDictionary<Guid, Channel<BifrostMessage>> _subs = new();

        public BifrostSubscription Add(string topic, int perSubscriberBuffer, Action onEmpty)
        {
            var id = Guid.NewGuid();

            var channel = Channel.CreateBounded<BifrostMessage>(new BoundedChannelOptions(perSubscriberBuffer)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            });

            _subs[id] = channel;

            void Unsubscribe()
            {
                if (_subs.TryRemove(id, out var ch))
                    ch.Writer.TryComplete();

                if (_subs.IsEmpty)
                    onEmpty();
            }

            return new BifrostSubscription(id, channel.Reader, Unsubscribe);
        }

        public void Publish(BifrostMessage message)
        {
            foreach (var kv in _subs)
            {
                kv.Value.Writer.TryWrite(message);
            }
        }

        public bool IsEmpty => _subs.IsEmpty;
    }
}