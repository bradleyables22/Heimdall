
using Heimdall.Server.Helpers;
using Microsoft.AspNetCore.Html;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Heimdall.Server
{
	internal sealed record BifrostMessage(
		string Topic,
		string Id,
		string Html,
		DateTimeOffset CreatedUtc,
		DateTimeOffset ExpiresUtc
	);
	public sealed class Bifrost
	{
		private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Channel<BifrostMessage>>> _subsByTopic
			= new(StringComparer.OrdinalIgnoreCase);


		// Subscribe returns (subscriptionId, reader, unsubscribe)
		internal (Guid id, ChannelReader<BifrostMessage> reader, Action unsubscribe) Subscribe( string topic, int perSubscriberBuffer = 64)
		{
			if (string.IsNullOrWhiteSpace(topic))
				throw new ArgumentException("Topic is required.", nameof(topic));

			var bucket = _subsByTopic.GetOrAdd(topic, _ => new ConcurrentDictionary<Guid, Channel<BifrostMessage>>());

			var id = Guid.NewGuid();

			// Bounded buffer so a slow client doesn't OOM the server
			var channel = Channel.CreateBounded<BifrostMessage>(new BoundedChannelOptions(perSubscriberBuffer)
			{
				SingleReader = true,
				SingleWriter = false,
				FullMode = BoundedChannelFullMode.DropOldest
			});

			bucket[id] = channel;

			void Unsubscribe()
			{
				if (_subsByTopic.TryGetValue(topic, out var b))
				{
					if (b.TryRemove(id, out var ch))
						ch.Writer.TryComplete();
					
					// Cleanup topic bucket if empty
					if (b.IsEmpty)
						_subsByTopic.TryRemove(topic, out _);
				}
			}

			return (id, channel.Reader, Unsubscribe);
		}

		/// <summary>
		/// Publishes HTML content to all active subscribers of the specified topic.
		/// 
		/// This method renders the provided <see cref="IHtmlContent"/> and delivers it
		/// to any clients currently subscribed to the topic, typically via
		/// Server-Sent Events (SSE).
		/// </summary>
		/// <param name="topic">
		/// The topic to which the content will be published.
		/// Only subscribers listening to this topic will receive the update.
		/// </param>
		/// <param name="content">
		/// The HTML content to publish.
		/// <param name="ttl">
		/// The time-to-live for the published content.
		/// 
		/// Content that exceeds this age may be dropped for slow or backlogged
		/// subscribers to avoid delivering stale UI updates.
		/// </param>
		/// <param name="ct">
		/// A cancellation token that can be used to cancel the publish operation
		/// before the content is delivered.
		/// </param>
		/// <returns>
		/// A <see cref="ValueTask"/> that completes once the content has been
		/// dispatched to current subscribers.
		/// </returns>
		/// <exception cref="ArgumentException">
		/// Thrown when <paramref name="topic"/> is null or empty.
		/// </exception>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="content"/> is null.
		/// </exception>
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

			foreach (var kv in bucket)
			{
				// best-effort fan-out; drop if the subscriber is slow (bounded channel will drop oldest)
				kv.Value.Writer.TryWrite(msg);
			}

			return ValueTask.CompletedTask;
		}

	}
}
