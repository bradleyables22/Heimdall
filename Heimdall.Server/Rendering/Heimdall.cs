using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Html;

namespace Heimdall.Server.Rendering
{
	/// <summary>
	/// Provides strongly-typed helpers for emitting Heimdall-compatible HTML attributes.
	/// </summary>
	/// <remarks>
	/// This class centralizes all Heimdall attribute names and provides safe, composable helpers
	/// for triggers, payload handling, swap behavior, and server-sent event configuration.
	/// </remarks>
	public static class HeimdallHtml
	{
		public static class Attrs
		{
			public const string Load = "heimdall-content-load";
			public const string Click = "heimdall-content-click";
			public const string Change = "heimdall-content-change";
			public const string Input = "heimdall-content-input";
			public const string Submit = "heimdall-content-submit";
			public const string KeyDown = "heimdall-content-keydown";
			public const string Blur = "heimdall-content-blur";
			public const string Hover = "heimdall-content-hover";
			public const string Visible = "heimdall-content-visible";
			public const string Scroll = "heimdall-content-scroll";

			public const string Target = "heimdall-content-target";
			public const string Swap = "heimdall-content-swap";
			public const string Disable = "heimdall-content-disable";
			public const string PreventDefault = "heimdall-prevent-default";

			public const string Payload = "heimdall-payload";
			public const string PayloadFrom = "heimdall-payload-from";
			public const string PayloadRef = "heimdall-payload-ref";

			public const string Debounce = "heimdall-debounce";
			public const string Key = "heimdall-key";
			public const string HoverDelay = "heimdall-hover-delay";
			public const string VisibleOnce = "heimdall-visible-once";
			public const string ScrollThreshold = "heimdall-scroll-threshold";
			public const string Poll = "heimdall-poll";

			public const string SseTopic = "heimdall-sse";
			public const string SseTopicAlias = "heimdall-sse-topic";
			public const string SseTarget = "heimdall-sse-target";
			public const string SseSwap = "heimdall-sse-swap";
			public const string SseEvent = "heimdall-sse-event";
			public const string SseDisable = "heimdall-sse-disable";

			public const string DataState = "data-heimdall-state";
			public const string DataStatePrefix = "data-heimdall-state-";
		}

		/// <summary>
		/// Represents a strongly-typed Heimdall action identifier.
		/// </summary>
		public readonly record struct ActionId(string Value)
		{
			public override string ToString() => Value ?? string.Empty;
			public static implicit operator string(ActionId id) => id.Value;
			public static implicit operator ActionId(string value) => new(value);
		}

		/// <summary>
		/// Defines supported Heimdall trigger types.
		/// </summary>
		public enum Trigger
		{
			Load,
			Click,
			Change,
			Input,
			Submit,
			KeyDown,
			Blur,
			Hover,
			Visible,
			Scroll
		}

		/// <summary>
		/// Defines DOM swap behaviors supported by Heimdall.
		/// </summary>
		public enum Swap
		{
			Inner,
			Outer,
			BeforeEnd,
			AfterBegin,
			None
		}

		/// <summary>
		/// Provides helpers for building payload source directives.
		/// </summary>
		public static class PayloadFrom
		{
			public const string ClosestForm = "closest-form";
			public const string Self = "self";
			public const string ClosestState = "closest-state";

			public static string ClosestStateKey(string key)
				=> string.IsNullOrWhiteSpace(key)
					? ClosestState
					: $"{ClosestState}:{key.Trim()}";

			public static string Ref(string globalPath) => $"ref:{globalPath}";
		}

		/// <summary>
		/// Emits a Heimdall trigger attribute for the specified trigger and action.
		/// </summary>
		public static Html.HtmlAttr On(Trigger trigger, ActionId action)
			=> Html.Attr(TriggerToAttr(trigger), action.Value);

		public static Html.HtmlAttr OnLoad(ActionId action) => Html.Attr(Attrs.Load, action.Value);
		public static Html.HtmlAttr OnClick(ActionId action) => Html.Attr(Attrs.Click, action.Value);
		public static Html.HtmlAttr OnChange(ActionId action) => Html.Attr(Attrs.Change, action.Value);
		public static Html.HtmlAttr OnInput(ActionId action) => Html.Attr(Attrs.Input, action.Value);
		public static Html.HtmlAttr OnSubmit(ActionId action) => Html.Attr(Attrs.Submit, action.Value);
		public static Html.HtmlAttr OnKeyDown(ActionId action) => Html.Attr(Attrs.KeyDown, action.Value);
		public static Html.HtmlAttr OnBlur(ActionId action) => Html.Attr(Attrs.Blur, action.Value);
		public static Html.HtmlAttr OnHover(ActionId action) => Html.Attr(Attrs.Hover, action.Value);
		public static Html.HtmlAttr OnVisible(ActionId action) => Html.Attr(Attrs.Visible, action.Value);
		public static Html.HtmlAttr OnScroll(ActionId action) => Html.Attr(Attrs.Scroll, action.Value);

		/// <summary>
		/// Specifies the DOM target that will receive the response content.
		/// </summary>
		public static Html.HtmlAttr Target(string selector) => Html.Attr(Attrs.Target, selector);

		/// <summary>
		/// Defines how returned content should be applied to the DOM.
		/// </summary>
		public static Html.HtmlAttr SwapMode(Swap swap) => Html.Attr(Attrs.Swap, SwapToString(swap));

		/// <summary>
		/// Disables the element while a request is in progress.
		/// </summary>
		public static Html.HtmlAttr Disable(bool on = true) => Html.Bool(Attrs.Disable, on);

		/// <summary>
		/// Prevents default browser behavior for the element.
		/// </summary>
		public static Html.HtmlAttr PreventDefault(bool on = true) => Html.Bool(Attrs.PreventDefault, on);

		/// <summary>
		/// Serializes and attaches a JSON payload to the element.
		/// </summary>
		public static Html.HtmlAttr Payload(object? payload, JsonSerializerOptions? options = null)
		{
			if (payload is null)
				return Html.HtmlAttr.Empty;

			var json = JsonSerializer.Serialize(payload, options);
			return Html.Attr(Attrs.Payload, json);
		}

		/// <summary>
		/// Forces an empty JSON payload.
		/// </summary>
		public static Html.HtmlAttr PayloadEmptyObject()
			=> Html.Attr(Attrs.Payload, "{}");

		/// <summary>
		/// Defines the payload source directive.
		/// </summary>
		public static Html.HtmlAttr PayloadFromDirective(string from)
			=> Html.Attr(Attrs.PayloadFrom, from);

		/// <summary>
		/// References a global payload object.
		/// </summary>
		public static Html.HtmlAttr PayloadRef(string globalPath)
			=> Html.Attr(Attrs.PayloadRef, globalPath);

		/// <summary>
		/// Uses closest-state as the payload source.
		/// </summary>
		public static Html.HtmlAttr PayloadFromClosestState(string? key = null)
			=> Html.Attr(Attrs.PayloadFrom,
				key is null ? PayloadFrom.ClosestState : PayloadFrom.ClosestStateKey(key));

		/// <summary>
		/// Writes a JSON state blob to the element.
		/// </summary>
		public static Html.HtmlAttr State(object? state, JsonSerializerOptions? options = null)
		{
			if (state is null)
				return Html.HtmlAttr.Empty;

			var json = JsonSerializer.Serialize(state, options);
			return Html.Attr(Attrs.DataState, json);
		}

		/// <summary>
		/// Writes a keyed JSON state blob to the element.
		/// </summary>
		public static Html.HtmlAttr State(string key, object? state, JsonSerializerOptions? options = null)
		{
			if (string.IsNullOrWhiteSpace(key))
				throw new ArgumentException("State key is required.", nameof(key));

			if (state is null)
				return Html.HtmlAttr.Empty;

			var json = JsonSerializer.Serialize(state, options);
			return Html.Attr($"{Attrs.DataStatePrefix}{key.Trim()}", json);
		}

		/// <summary>
		/// Writes raw JSON state without serialization.
		/// </summary>
		public static Html.HtmlAttr StateJson(string json)
			=> Html.Attr(Attrs.DataState, json ?? "null");

		/// <summary>
		/// Writes raw keyed JSON state without serialization.
		/// </summary>
		public static Html.HtmlAttr StateJson(string key, string json)
		{
			if (string.IsNullOrWhiteSpace(key))
				throw new ArgumentException("State key is required.", nameof(key));

			return Html.Attr($"{Attrs.DataStatePrefix}{key.Trim()}", json ?? "null");
		}

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

		/// <summary>
		/// Enables SSE binding for a given topic.
		/// </summary>
		public static Html.HtmlAttr SseTopic(string topic)
			=> Html.Attr(Attrs.SseTopic, topic);

		public static Html.HtmlAttr SseTopicAlias(string topic)
			=> Html.Attr(Attrs.SseTopicAlias, topic);

		public static Html.HtmlAttr SseTarget(string selector)
			=> Html.Attr(Attrs.SseTarget, selector);

		public static Html.HtmlAttr SseSwapMode(Swap swap)
			=> Html.Attr(Attrs.SseSwap, SwapToString(swap));

		public static Html.HtmlAttr SseEvent(string eventName)
			=> Html.Attr(Attrs.SseEvent, eventName);

		public static Html.HtmlAttr SseDisable(bool on = true)
			=> Html.Bool(Attrs.SseDisable, on);

		/// <summary>
		/// Creates an out-of-band invocation element.
		/// </summary>
		public static IHtmlContent Invocation(
			string targetSelector,
			Swap swap = Swap.Inner,
			IHtmlContent? payload = null,
			bool wrapInTemplate = false)
		{
			if (string.IsNullOrWhiteSpace(targetSelector))
				throw new ArgumentException("Invocation target selector is required.", nameof(targetSelector));

			var swapStr = SwapToString(swap);

			if (payload is null)
			{
				return Html.Tag("invocation",
					Html.Attr(Attrs.Target, targetSelector),
					Html.Attr(Attrs.Swap, swapStr));
			}

			if (!wrapInTemplate)
			{
				return Html.Tag("invocation",
					Html.Attr(Attrs.Target, targetSelector),
					Html.Attr(Attrs.Swap, swapStr),
					payload);
			}

			return Html.Tag("invocation",
				Html.Attr(Attrs.Target, targetSelector),
				Html.Attr(Attrs.Swap, swapStr),
				Html.Tag("template", payload));
		}

		private static string TriggerToAttr(Trigger trigger) => trigger switch
		{
			Trigger.Load => Attrs.Load,
			Trigger.Click => Attrs.Click,
			Trigger.Change => Attrs.Change,
			Trigger.Input => Attrs.Input,
			Trigger.Submit => Attrs.Submit,
			Trigger.KeyDown => Attrs.KeyDown,
			Trigger.Blur => Attrs.Blur,
			Trigger.Hover => Attrs.Hover,
			Trigger.Visible => Attrs.Visible,
			Trigger.Scroll => Attrs.Scroll,
			_ => throw new ArgumentOutOfRangeException(nameof(trigger))
		};

		private static string SwapToString(Swap swap) => swap switch
		{
			Swap.Inner => "inner",
			Swap.Outer => "outer",
			Swap.BeforeEnd => "beforeend",
			Swap.AfterBegin => "afterbegin",
			Swap.None => "none",
			_ => "inner"
		};
	}
}