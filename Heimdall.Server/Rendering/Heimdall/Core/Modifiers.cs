
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
		/// Prevents Heimdall delegated trigger resolution from crossing this element for the specified triggers.
		/// </summary>
		public static Html.HtmlAttr Ignore(params Trigger[] triggers)
			=> Html.Attr(Attrs.Ignore, TriggerListToString(triggers));

		/// <summary>
		/// Prevents Heimdall delegated trigger resolution from crossing this element for all triggers.
		/// </summary>
		public static Html.HtmlAttr IgnoreAll()
			=> Html.Attr(Attrs.Ignore, "*");

		/// <summary>
		/// Emits a raw Heimdall ignore trigger list.
		/// </summary>
		public static Html.HtmlAttr Ignore(string triggerList)
			=> Html.Attr(Attrs.Ignore, triggerList);

		/// <summary>
		/// Sets the delegated trigger matching scope.
		/// </summary>
		public static Html.HtmlAttr Scope(EventScope scope)
			=> Html.Attr(Attrs.Scope, EventScopeToString(scope));

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

		private static string EventScopeToString(EventScope scope) => scope switch
		{
			EventScope.Self => "self",
			EventScope.Closest => "closest",
			_ => "closest"
		};

		private static string TriggerListToString(IReadOnlyCollection<Trigger>? triggers)
		{
			if (triggers is null || triggers.Count == 0)
				return "*";

			return string.Join(" ", triggers.Select(TriggerToToken));
		}

		private static string TriggerToToken(Trigger trigger) => trigger switch
		{
			Trigger.Load => "load",
			Trigger.Click => "click",
			Trigger.Change => "change",
			Trigger.Input => "input",
			Trigger.Submit => "submit",
			Trigger.KeyDown => "keydown",
			Trigger.Blur => "blur",
			Trigger.Hover => "hover",
			Trigger.Visible => "visible",
			Trigger.Scroll => "scroll",
			_ => throw new ArgumentOutOfRangeException(nameof(trigger))
		};
	}
}
