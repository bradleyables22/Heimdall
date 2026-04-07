
using Heimdall.Server.Helpers;
using Heimdall.Server.Utilities;
using Microsoft.AspNetCore.Html;
using System.Collections.Concurrent;

namespace Heimdall.Server
{
    /// <summary>
    /// Provides a publish-subscribe mechanism for delivering HTML content to clients subscribed to specific topics.
    /// This class enables broadcasting rendered HTML updates, such as UI fragments, to multiple subscribers in real
    /// time, typically using Server-Sent Events (SSE).
    /// </summary>
    /// <remarks>Bifrost manages topic-based subscriptions and ensures that published content is efficiently
    /// delivered to all active subscribers of a topic. Content can be published with a time-to-live (TTL) to prevent
    /// stale updates from being sent to slow or disconnected clients. Instances of this class are thread-safe and
    /// intended for use in web applications that require real-time UI updates.</remarks>
	public sealed class Bifrost
	{
        private readonly ConcurrentDictionary<string, TopicSubscriptions> _subsByTopic
            = new(StringComparer.OrdinalIgnoreCase);


        // Subscribe returns (subscriptionId, reader, unsubscribe)
        internal BifrostSubscription Subscribe(string topic, int perSubscriberBuffer = 64)
        {
            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("Topic is required.", nameof(topic));

            var bucket = _subsByTopic.GetOrAdd(topic, _ => new TopicSubscriptions());

            return bucket.Add(
                topic,
                perSubscriberBuffer,
                onEmpty: () => _subsByTopic.TryRemove(topic, out _)
            );
        }
        /// <summary>
        /// Publishes an HTML message to the specified topic with a given time-to-live (TTL) duration.
        /// </summary>
        /// <param name="topic">The topic to which the message will be published. Cannot be null, empty, or consist only of white-space
        /// characters.</param>
        /// <param name="content">The HTML content to publish. Cannot be null.</param>
        /// <param name="ttl">The duration for which the message remains valid. If less than or equal to zero, a default TTL of 5 seconds
        /// is used.</param>
        /// <param name="ct">A cancellation token that can be used to cancel the publish operation.</param>
        /// <returns>A ValueTask that represents the asynchronous publish operation.</returns>
        /// <exception cref="ArgumentException">Thrown if topic is null, empty, or consists only of white-space characters.</exception>
        /// <exception cref="ArgumentNullException">Thrown if content is null.</exception>
        public ValueTask PublishAsync(string topic, IHtmlContent content, TimeSpan ttl, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("Topic is required.", nameof(topic));

            if (content is null)
                throw new ArgumentNullException(nameof(content));

            if (ttl <= TimeSpan.Zero)
                ttl = TimeSpan.FromSeconds(5);

            var html = content.RenderHtml();
            var now = DateTimeOffset.UtcNow;

            var msg = new BifrostMessage(
                Topic: topic,
                Id: Guid.NewGuid().ToString("N"),
                Html: html,
                CreatedUtc: now,
                ExpiresUtc: now.Add(ttl)
            );

            if (!_subsByTopic.TryGetValue(topic, out var bucket) || bucket.IsEmpty)
                return ValueTask.CompletedTask;

            bucket.Publish(msg);

            return ValueTask.CompletedTask;
        }

    }
}
