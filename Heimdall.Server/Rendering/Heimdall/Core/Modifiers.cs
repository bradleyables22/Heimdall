
using System.Globalization;

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
		/// Disables the element while a request is in progress.
		/// </summary>
		public static Html.HtmlAttr Disable(bool on = true) => Html.Bool(Attrs.Disable, on);

		/// <summary>
		/// Prevents default browser behavior for the element.
		/// </summary>
		public static Html.HtmlAttr PreventDefault(bool on = true) => Html.Bool(Attrs.PreventDefault, on);
		/// <summary>
		/// Adds a debounce delay to a trigger.
		/// </summary>
		public static Html.HtmlAttr DebounceMs(int ms)
			=> Html.Attr(Attrs.Debounce, Math.Max(0, ms).ToString(CultureInfo.InvariantCulture));

		/// <summary>
		/// Filters key-based triggers.
		/// </summary>
		public static Html.HtmlAttr Key(string keySpec)
			=> Html.Attr(Attrs.Key, keySpec);

		/// <summary>
		/// Adds a delay before hover triggers fire.
		/// </summary>
		public static Html.HtmlAttr HoverDelayMs(int ms)
			=> Html.Attr(Attrs.HoverDelay, Math.Max(0, ms).ToString(CultureInfo.InvariantCulture));

		/// <summary>
		/// Ensures a visible trigger runs only once.
		/// </summary>
		public static Html.HtmlAttr VisibleOnce(bool on = true)
			=> Html.Bool(Attrs.VisibleOnce, on);

		/// <summary>
		/// Sets the scroll trigger threshold.
		/// </summary>
		public static Html.HtmlAttr ScrollThresholdPx(int px)
			=> Html.Attr(Attrs.ScrollThreshold, Math.Max(0, px).ToString(CultureInfo.InvariantCulture));

		/// <summary>
		/// Configures polling interval for repeated execution.
		/// </summary>
		public static Html.HtmlAttr PollMs(int ms)
			=> Html.Attr(Attrs.Poll, Math.Max(0, ms).ToString(CultureInfo.InvariantCulture));
	}
}
