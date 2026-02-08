using Microsoft.AspNetCore.Html;

namespace Heimdall.Server
{
	/// <summary>
	/// Represents a unit of HTML content to be published to a Bifrost topic.
	/// 
	/// A <see cref="BifrostContent"/> instance encapsulates the destination topic,
	/// the HTML payload to deliver to subscribers, and the time window during which
	/// the content is considered valid.
	/// </summary>
	public sealed class BifrostContent
	{
		/// <summary>
		/// Creates a new <see cref="BifrostContent"/> instance.
		/// </summary>
		/// <param name="topic">
		/// The topic to which this content will be published.
		/// Topics act as logical channels that subscribers listen to
		/// (for example: <c>"job:123"</c> or <c>"user:abc:notifications"</c>).
		/// </param>
		/// <param name="content">
		/// The HTML content to deliver to subscribers.
		/// 
		/// This content may include Heimdall out-of-band (<c>&lt;invocation&gt;</c>)
		/// directives and will be rendered to a string using the configured
		/// <see cref="System.Text.Encodings.Web.HtmlEncoder"/>.
		/// </param>
		/// <param name="timeToLive">
		/// The duration for which this content is considered valid.
		/// 
		/// Expired content may be dropped if a subscriber is slow or temporarily
		/// disconnected. This helps prevent stale UI updates from being delivered.
		/// </param>
		public BifrostContent(string topic, IHtmlContent content, TimeSpan timeToLive)
		{
			Topic = topic;
			Content = content;
			TimeToLive = timeToLive;
		}

		/// <summary>
		/// The topic to which this content will be published.
		/// 
		/// Topics are matched case-insensitively and determine which subscribers
		/// receive the content.
		/// </summary>
		public string Topic { get; init; }

		/// <summary>
		/// The HTML payload to publish to the topic.
		/// 
		/// This content is rendered server-side and streamed to subscribers,
		/// typically over Server-Sent Events (SSE).
		/// </summary>
		public IHtmlContent Content { get; init; }

		/// <summary>
		/// The maximum age of this content before it is considered expired.
		/// 
		/// This value is used to prevent delayed or backlogged subscribers
		/// from receiving outdated updates.
		/// </summary>
		public TimeSpan TimeToLive { get; init; }
	}
}
