
namespace Heimdall.Server.Rendering
{
	/// <summary>
	/// Provides strongly-typed helpers for emitting Heimdall-compatible HTML attributes.
	/// </summary>
	/// <remarks>
	/// This class centralizes all Heimdall attribute names and provides safe, composable helpers
	/// for triggers, payload handling, swap behavior, and server-sent event configuration.
	/// </remarks>
	public static partial class HeimdallHtml
	{
		/// <summary>
		/// Enables SSE binding for a given topic.
		/// </summary>
		public static Html.HtmlAttr SseTopic(string topic)
			=> Html.Attr(Attrs.SseTopic, topic);
		/// <summary>
		/// Defines an alias for the SSE topic, allowing multiple elements to share the same topic configuration without repeating the topic name.
		/// </summary>
		/// <param name="topic"></param>
		/// <returns></returns>
		public static Html.HtmlAttr SseTopicAlias(string topic)
			=> Html.Attr(Attrs.SseTopicAlias, topic);
		/// <summary>
		/// Specifies a CSS selector for the target element(s) to update when an SSE message is received. This allows for dynamic content updates based on server-sent events without requiring a full page reload.
		/// </summary>
		/// <param name="selector"></param>
		/// <returns></returns>
		public static Html.HtmlAttr SseTarget(string selector)
			=> Html.Attr(Attrs.SseTarget, selector);

		/// <summary>
		/// Defines how the content should be swapped when an SSE message is received. The Swap enum provides options for inner, outer, beforeend, afterbegin, or none, allowing developers to control the DOM manipulation behavior precisely when updating content in response to server-sent events.
		/// </summary>
		/// <param name="swap"></param>
		/// <returns></returns>
		public static Html.HtmlAttr SseSwapMode(Swap swap)
			=> Html.Attr(Attrs.SseSwap, SwapToString(swap));

		/// <summary>
		/// Specifies the name of the event to listen for on the server-sent event stream. This allows developers to handle different types of events with specific client-side logic, enabling more granular control over how the application responds to various server-sent events.
		/// </summary>
		/// <param name="eventName"></param>
		/// <returns></returns>
		public static Html.HtmlAttr SseEvent(string eventName)
			=> Html.Attr(Attrs.SseEvent, eventName);

		/// <summary>
		/// Disables SSE updates for the element, preventing it from being updated in response to server-sent events. This can be useful for elements that should not change dynamically or for temporarily pausing updates without removing the SSE configuration entirely.
		/// </summary>
		/// <param name="on"></param>
		/// <returns></returns>
		public static Html.HtmlAttr SseDisable(bool on = true)
			=> Html.Bool(Attrs.SseDisable, on);
	}
}
