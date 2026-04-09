using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Html;

namespace Heimdall.Server.Rendering
{
	/// <summary>
	/// Provides strongly-typed helpers for emitting Heimdall-compatible HTML attributes.
	/// </summary>
	/// <remarks>
	/// This class centralizes Heimdall attribute names and provides composable helpers for triggers,
	/// payload handling, swap behavior, state emission, and server-sent event configuration.
	/// </remarks>
	public static class HeimdallHtml
	{
		/// <summary>
		/// Defines Heimdall HTML attribute names used for content events, payloads, state, and SSE configuration.
		/// </summary>
		public static class Attrs
		{
			/// <summary>
			/// The attribute that binds an action to the load trigger.
			/// </summary>
			public const string Load = "heimdall-content-load";

			/// <summary>
			/// The attribute that binds an action to the click trigger.
			/// </summary>
			public const string Click = "heimdall-content-click";

			/// <summary>
			/// The attribute that binds an action to the change trigger.
			/// </summary>
			public const string Change = "heimdall-content-change";

			/// <summary>
			/// The attribute that binds an action to the input trigger.
			/// </summary>
			public const string Input = "heimdall-content-input";

			/// <summary>
			/// The attribute that binds an action to the submit trigger.
			/// </summary>
			public const string Submit = "heimdall-content-submit";

			/// <summary>
			/// The attribute that binds an action to the keydown trigger.
			/// </summary>
			public const string KeyDown = "heimdall-content-keydown";

			/// <summary>
			/// The attribute that binds an action to the blur trigger.
			/// </summary>
			public const string Blur = "heimdall-content-blur";

			/// <summary>
			/// The attribute that binds an action to the hover trigger.
			/// </summary>
			public const string Hover = "heimdall-content-hover";

			/// <summary>
			/// The attribute that binds an action to the visible trigger.
			/// </summary>
			public const string Visible = "heimdall-content-visible";

			/// <summary>
			/// The attribute that binds an action to the scroll trigger.
			/// </summary>
			public const string Scroll = "heimdall-content-scroll";

			/// <summary>
			/// The attribute that identifies the DOM target selector for content updates.
			/// </summary>
			public const string Target = "heimdall-content-target";

			/// <summary>
			/// The attribute that specifies how returned content is swapped into the DOM.
			/// </summary>
			public const string Swap = "heimdall-content-swap";

			/// <summary>
			/// The attribute that disables an element while a request is in progress.
			/// </summary>
			public const string Disable = "heimdall-content-disable";

			/// <summary>
			/// The attribute that prevents the browser's default behavior for an interaction.
			/// </summary>
			public const string PreventDefault = "heimdall-prevent-default";

			/// <summary>
			/// The attribute that contains an inline JSON payload.
			/// </summary>
			public const string Payload = "heimdall-payload";

			/// <summary>
			/// The attribute that declares where a payload should be sourced from.
			/// </summary>
			public const string PayloadFrom = "heimdall-payload-from";

			/// <summary>
			/// The attribute that references a global payload object.
			/// </summary>
			public const string PayloadRef = "heimdall-payload-ref";

			/// <summary>
			/// The attribute that configures debounce timing in milliseconds.
			/// </summary>
			public const string Debounce = "heimdall-debounce";

			/// <summary>
			/// The attribute that filters key-based triggers.
			/// </summary>
			public const string Key = "heimdall-key";

			/// <summary>
			/// The attribute that configures hover delay in milliseconds.
			/// </summary>
			public const string HoverDelay = "heimdall-hover-delay";

			/// <summary>
			/// The attribute that limits the visible trigger to a single execution.
			/// </summary>
			public const string VisibleOnce = "heimdall-visible-once";

			/// <summary>
			/// The attribute that configures the scroll threshold in pixels.
			/// </summary>
			public const string ScrollThreshold = "heimdall-scroll-threshold";

			/// <summary>
			/// The attribute that configures polling frequency in milliseconds.
			/// </summary>
			public const string Poll = "heimdall-poll";

			/// <summary>
			/// The attribute that binds an element to an SSE topic.
			/// </summary>
			public const string SseTopic = "heimdall-sse";

			/// <summary>
			/// The alternate attribute name that binds an element to an SSE topic.
			/// </summary>
			public const string SseTopicAlias = "heimdall-sse-topic";

			/// <summary>
			/// The attribute that identifies the DOM target selector for SSE updates.
			/// </summary>
			public const string SseTarget = "heimdall-sse-target";

			/// <summary>
			/// The attribute that specifies how SSE content is swapped into the DOM.
			/// </summary>
			public const string SseSwap = "heimdall-sse-swap";

			/// <summary>
			/// The attribute that filters SSE messages by event name.
			/// </summary>
			public const string SseEvent = "heimdall-sse-event";

			/// <summary>
			/// The attribute that disables SSE handling for an element.
			/// </summary>
			public const string SseDisable = "heimdall-sse-disable";

			/// <summary>
			/// The attribute that stores an unkeyed JSON state blob on an element.
			/// </summary>
			public const string DataState = "data-heimdall-state";

			/// <summary>
			/// The prefix used for keyed JSON state attributes.
			/// </summary>
			public const string DataStatePrefix = "data-heimdall-state-";
		}

		/// <summary>
		/// Represents a strongly-typed Heimdall action identifier.
		/// </summary>
		/// <param name="Value">The underlying action identifier value.</param>
		public readonly record struct ActionId(string Value)
		{
			/// <summary>
			/// Returns the underlying action identifier value.
			/// </summary>
			/// <returns>The action identifier value, or an empty string when the value is <see langword="null" />.</returns>
			public override string ToString() => Value ?? string.Empty;

			/// <summary>
			/// Converts an <see cref="ActionId" /> to its underlying string value.
			/// </summary>
			/// <param name="id">The action identifier to convert.</param>
			public static implicit operator string(ActionId id) => id.Value;

			/// <summary>
			/// Converts a string to an <see cref="ActionId" />.
			/// </summary>
			/// <param name="value">The string value to wrap.</param>
			public static implicit operator ActionId(string value) => new(value);
		}

		/// <summary>
		/// Defines supported Heimdall trigger types.
		/// </summary>
		public enum Trigger
		{
			/// <summary>
			/// Triggers when the element is loaded.
			/// </summary>
			Load,

			/// <summary>
			/// Triggers when the element is clicked.
			/// </summary>
			Click,

			/// <summary>
			/// Triggers when the element value changes.
			/// </summary>
			Change,

			/// <summary>
			/// Triggers when input is received.
			/// </summary>
			Input,

			/// <summary>
			/// Triggers when a form is submitted.
			/// </summary>
			Submit,

			/// <summary>
			/// Triggers when a key is pressed down.
			/// </summary>
			KeyDown,

			/// <summary>
			/// Triggers when the element loses focus.
			/// </summary>
			Blur,

			/// <summary>
			/// Triggers when the element is hovered.
			/// </summary>
			Hover,

			/// <summary>
			/// Triggers when the element becomes visible.
			/// </summary>
			Visible,

			/// <summary>
			/// Triggers when the element is scrolled.
			/// </summary>
			Scroll
		}

		/// <summary>
		/// Defines DOM swap behaviors supported by Heimdall.
		/// </summary>
		public enum Swap
		{
			/// <summary>
			/// Replaces the target element's inner content.
			/// </summary>
			Inner,

			/// <summary>
			/// Replaces the entire target element.
			/// </summary>
			Outer,

			/// <summary>
			/// Appends content to the end of the target element.
			/// </summary>
			BeforeEnd,

			/// <summary>
			/// Inserts content at the beginning of the target element.
			/// </summary>
			AfterBegin,

			/// <summary>
			/// Does not perform a DOM swap.
			/// </summary>
			None
		}

		/// <summary>
		/// Provides helpers for building payload source directives.
		/// </summary>
		public static class PayloadFrom
		{
			/// <summary>
			/// Indicates that payload data should be read from the closest form ancestor.
			/// </summary>
			public const string ClosestForm = "closest-form";

			/// <summary>
			/// Indicates that payload data should be read from the current element.
			/// </summary>
			public const string Self = "self";

			/// <summary>
			/// Indicates that payload data should be read from the closest available state container.
			/// </summary>
			public const string ClosestState = "closest-state";

			/// <summary>
			/// Creates a <c>closest-state</c> directive, optionally scoped to a specific state key.
			/// </summary>
			/// <param name="key">The optional state key to append.</param>
			/// <returns>
			/// <see cref="ClosestState" /> when <paramref name="key" /> is blank; otherwise a keyed
			/// <c>closest-state:{key}</c> directive.
			/// </returns>
			public static string ClosestStateKey(string key)
				=> string.IsNullOrWhiteSpace(key)
					? ClosestState
					: $"{ClosestState}:{key.Trim()}";

			/// <summary>
			/// Creates a payload reference directive for a global path.
			/// </summary>
			/// <param name="globalPath">The global object path to reference.</param>
			/// <returns>A <c>ref:</c> directive for the specified global path.</returns>
			public static string Ref(string globalPath) => $"ref:{globalPath}";
		}

		/// <summary>
		/// Emits a Heimdall trigger attribute for the specified trigger and action.
		/// </summary>
		/// <param name="trigger">The trigger to bind.</param>
		/// <param name="action">The action identifier to invoke.</param>
		/// <returns>An HTML attribute representing the trigger binding.</returns>
		public static Html.HtmlAttr On(Trigger trigger, ActionId action)
			=> Html.Attr(TriggerToAttr(trigger), action.Value);

		/// <summary>
		/// Emits a load trigger attribute for the specified action.
		/// </summary>
		/// <param name="action">The action identifier to invoke.</param>
		/// <returns>An HTML attribute representing the load trigger binding.</returns>
		public static Html.HtmlAttr OnLoad(ActionId action) => Html.Attr(Attrs.Load, action.Value);

		/// <summary>
		/// Emits a click trigger attribute for the specified action.
		/// </summary>
		/// <param name="action">The action identifier to invoke.</param>
		/// <returns>An HTML attribute representing the click trigger binding.</returns>
		public static Html.HtmlAttr OnClick(ActionId action) => Html.Attr(Attrs.Click, action.Value);

		/// <summary>
		/// Emits a change trigger attribute for the specified action.
		/// </summary>
		/// <param name="action">The action identifier to invoke.</param>
		/// <returns>An HTML attribute representing the change trigger binding.</returns>
		public static Html.HtmlAttr OnChange(ActionId action) => Html.Attr(Attrs.Change, action.Value);

		/// <summary>
		/// Emits an input trigger attribute for the specified action.
		/// </summary>
		/// <param name="action">The action identifier to invoke.</param>
		/// <returns>An HTML attribute representing the input trigger binding.</returns>
		public static Html.HtmlAttr OnInput(ActionId action) => Html.Attr(Attrs.Input, action.Value);

		/// <summary>
		/// Emits a submit trigger attribute for the specified action.
		/// </summary>
		/// <param name="action">The action identifier to invoke.</param>
		/// <returns>An HTML attribute representing the submit trigger binding.</returns>
		public static Html.HtmlAttr OnSubmit(ActionId action) => Html.Attr(Attrs.Submit, action.Value);

		/// <summary>
		/// Emits a keydown trigger attribute for the specified action.
		/// </summary>
		/// <param name="action">The action identifier to invoke.</param>
		/// <returns>An HTML attribute representing the keydown trigger binding.</returns>
		public static Html.HtmlAttr OnKeyDown(ActionId action) => Html.Attr(Attrs.KeyDown, action.Value);

		/// <summary>
		/// Emits a blur trigger attribute for the specified action.
		/// </summary>
		/// <param name="action">The action identifier to invoke.</param>
		/// <returns>An HTML attribute representing the blur trigger binding.</returns>
		public static Html.HtmlAttr OnBlur(ActionId action) => Html.Attr(Attrs.Blur, action.Value);

		/// <summary>
		/// Emits a hover trigger attribute for the specified action.
		/// </summary>
		/// <param name="action">The action identifier to invoke.</param>
		/// <returns>An HTML attribute representing the hover trigger binding.</returns>
		public static Html.HtmlAttr OnHover(ActionId action) => Html.Attr(Attrs.Hover, action.Value);

		/// <summary>
		/// Emits a visible trigger attribute for the specified action.
		/// </summary>
		/// <param name="action">The action identifier to invoke.</param>
		/// <returns>An HTML attribute representing the visible trigger binding.</returns>
		public static Html.HtmlAttr OnVisible(ActionId action) => Html.Attr(Attrs.Visible, action.Value);

		/// <summary>
		/// Emits a scroll trigger attribute for the specified action.
		/// </summary>
		/// <param name="action">The action identifier to invoke.</param>
		/// <returns>An HTML attribute representing the scroll trigger binding.</returns>
		public static Html.HtmlAttr OnScroll(ActionId action) => Html.Attr(Attrs.Scroll, action.Value);

		/// <summary>
		/// Specifies the DOM target that will receive response content.
		/// </summary>
		/// <param name="selector">The CSS selector identifying the target element.</param>
		/// <returns>An HTML attribute representing the target selector.</returns>
		public static Html.HtmlAttr Target(string selector) => Html.Attr(Attrs.Target, selector);

		/// <summary>
		/// Defines how returned content should be applied to the DOM.
		/// </summary>
		/// <param name="swap">The swap behavior to apply.</param>
		/// <returns>An HTML attribute representing the swap mode.</returns>
		public static Html.HtmlAttr SwapMode(Swap swap) => Html.Attr(Attrs.Swap, SwapToString(swap));

		/// <summary>
		/// Disables the element while a request is in progress.
		/// </summary>
		/// <param name="on">
		/// <see langword="true" /> to emit the disable attribute; otherwise, <see langword="false" />.
		/// </param>
		/// <returns>An HTML attribute representing the disabled-request behavior.</returns>
		public static Html.HtmlAttr Disable(bool on = true) => Html.Bool(Attrs.Disable, on);

		/// <summary>
		/// Prevents the browser's default behavior for the element interaction.
		/// </summary>
		/// <param name="on">
		/// <see langword="true" /> to emit the prevent-default attribute; otherwise, <see langword="false" />.
		/// </param>
		/// <returns>An HTML attribute representing the prevent-default behavior.</returns>
		public static Html.HtmlAttr PreventDefault(bool on = true) => Html.Bool(Attrs.PreventDefault, on);

		/// <summary>
		/// Serializes and attaches a JSON payload to the element.
		/// </summary>
		/// <param name="payload">The payload object to serialize.</param>
		/// <param name="options">Optional JSON serializer options.</param>
		/// <returns>
		/// An HTML attribute containing the serialized payload, or <see cref="Html.HtmlAttr.Empty" />
		/// when <paramref name="payload" /> is <see langword="null" />.
		/// </returns>
		public static Html.HtmlAttr Payload(object? payload, JsonSerializerOptions? options = null)
		{
			if (payload is null)
				return Html.HtmlAttr.Empty;

			var json = JsonSerializer.Serialize(payload, options);
			return Html.Attr(Attrs.Payload, json);
		}

		/// <summary>
		/// Forces an empty JSON object payload.
		/// </summary>
		/// <returns>An HTML attribute containing an empty JSON object.</returns>
		public static Html.HtmlAttr PayloadEmptyObject()
			=> Html.Attr(Attrs.Payload, "{}");

		/// <summary>
		/// Defines the payload source directive.
		/// </summary>
		/// <param name="from">The payload source directive value.</param>
		/// <returns>An HTML attribute representing the payload source.</returns>
		public static Html.HtmlAttr PayloadFromDirective(string from)
			=> Html.Attr(Attrs.PayloadFrom, from);

		/// <summary>
		/// References a global payload object.
		/// </summary>
		/// <param name="globalPath">The global object path to reference.</param>
		/// <returns>An HTML attribute representing the global payload reference.</returns>
		public static Html.HtmlAttr PayloadRef(string globalPath)
			=> Html.Attr(Attrs.PayloadRef, globalPath);

		/// <summary>
		/// Configures the payload to be sourced from the closest state container.
		/// </summary>
		/// <param name="key">An optional state key used to scope the closest-state lookup.</param>
		/// <returns>An HTML attribute representing the closest-state payload source directive.</returns>
		public static Html.HtmlAttr PayloadFromClosestState(string? key = null)
			=> Html.Attr(Attrs.PayloadFrom,
				key is null ? PayloadFrom.ClosestState : PayloadFrom.ClosestStateKey(key));

		/// <summary>
		/// Writes a JSON state blob to the element.
		/// </summary>
		/// <param name="state">The state object to serialize.</param>
		/// <param name="options">Optional JSON serializer options.</param>
		/// <returns>
		/// An HTML attribute containing the serialized state, or <see cref="Html.HtmlAttr.Empty" />
		/// when <paramref name="state" /> is <see langword="null" />.
		/// </returns>
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
		/// <param name="key">The state key to write under.</param>
		/// <param name="state">The state object to serialize.</param>
		/// <param name="options">Optional JSON serializer options.</param>
		/// <returns>
		/// An HTML attribute containing the serialized keyed state, or <see cref="Html.HtmlAttr.Empty" />
		/// when <paramref name="state" /> is <see langword="null" />.
		/// </returns>
		/// <exception cref="ArgumentException"><paramref name="key" /> is null, empty, or whitespace.</exception>
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
		/// <param name="json">The raw JSON value to write.</param>
		/// <returns>An HTML attribute containing the raw JSON state.</returns>
		public static Html.HtmlAttr StateJson(string json)
			=> Html.Attr(Attrs.DataState, json ?? "null");

		/// <summary>
		/// Writes raw keyed JSON state without serialization.
		/// </summary>
		/// <param name="key">The state key to write under.</param>
		/// <param name="json">The raw JSON value to write.</param>
		/// <returns>An HTML attribute containing the raw keyed JSON state.</returns>
		/// <exception cref="ArgumentException"><paramref name="key" /> is null, empty, or whitespace.</exception>
		public static Html.HtmlAttr StateJson(string key, string json)
		{
			if (string.IsNullOrWhiteSpace(key))
				throw new ArgumentException("State key is required.", nameof(key));

			return Html.Attr($"{Attrs.DataStatePrefix}{key.Trim()}", json ?? "null");
		}

		/// <summary>
		/// Adds a debounce delay to a trigger.
		/// </summary>
		/// <param name="ms">The debounce duration in milliseconds.</param>
		/// <returns>An HTML attribute representing the debounce duration.</returns>
		public static Html.HtmlAttr DebounceMs(int ms)
			=> Html.Attr(Attrs.Debounce, Math.Max(0, ms).ToString(CultureInfo.InvariantCulture));

		/// <summary>
		/// Filters key-based triggers.
		/// </summary>
		/// <param name="keySpec">The key filter specification.</param>
		/// <returns>An HTML attribute representing the key filter.</returns>
		public static Html.HtmlAttr Key(string keySpec)
			=> Html.Attr(Attrs.Key, keySpec);

		/// <summary>
		/// Adds a delay before hover triggers fire.
		/// </summary>
		/// <param name="ms">The hover delay in milliseconds.</param>
		/// <returns>An HTML attribute representing the hover delay.</returns>
		public static Html.HtmlAttr HoverDelayMs(int ms)
			=> Html.Attr(Attrs.HoverDelay, Math.Max(0, ms).ToString(CultureInfo.InvariantCulture));

		/// <summary>
		/// Ensures a visible trigger runs only once.
		/// </summary>
		/// <param name="on">
		/// <see langword="true" /> to emit the visible-once attribute; otherwise, <see langword="false" />.
		/// </param>
		/// <returns>An HTML attribute representing the visible-once behavior.</returns>
		public static Html.HtmlAttr VisibleOnce(bool on = true)
			=> Html.Bool(Attrs.VisibleOnce, on);

		/// <summary>
		/// Sets the scroll trigger threshold.
		/// </summary>
		/// <param name="px">The scroll threshold in pixels.</param>
		/// <returns>An HTML attribute representing the scroll threshold.</returns>
		public static Html.HtmlAttr ScrollThresholdPx(int px)
			=> Html.Attr(Attrs.ScrollThreshold, Math.Max(0, px).ToString(CultureInfo.InvariantCulture));

		/// <summary>
		/// Configures polling interval for repeated execution.
		/// </summary>
		/// <param name="ms">The polling interval in milliseconds.</param>
		/// <returns>An HTML attribute representing the polling interval.</returns>
		public static Html.HtmlAttr PollMs(int ms)
			=> Html.Attr(Attrs.Poll, Math.Max(0, ms).ToString(CultureInfo.InvariantCulture));

		/// <summary>
		/// Enables SSE binding for a given topic.
		/// </summary>
		/// <param name="topic">The SSE topic name.</param>
		/// <returns>An HTML attribute representing the SSE topic binding.</returns>
		public static Html.HtmlAttr SseTopic(string topic)
			=> Html.Attr(Attrs.SseTopic, topic);

		/// <summary>
		/// Enables SSE binding using the alternate topic attribute name.
		/// </summary>
		/// <param name="topic">The SSE topic name.</param>
		/// <returns>An HTML attribute representing the SSE topic binding.</returns>
		public static Html.HtmlAttr SseTopicAlias(string topic)
			=> Html.Attr(Attrs.SseTopicAlias, topic);

		/// <summary>
		/// Specifies the DOM target that will receive SSE response content.
		/// </summary>
		/// <param name="selector">The CSS selector identifying the SSE target element.</param>
		/// <returns>An HTML attribute representing the SSE target selector.</returns>
		public static Html.HtmlAttr SseTarget(string selector)
			=> Html.Attr(Attrs.SseTarget, selector);

		/// <summary>
		/// Defines how SSE content should be applied to the DOM.
		/// </summary>
		/// <param name="swap">The swap behavior to apply.</param>
		/// <returns>An HTML attribute representing the SSE swap mode.</returns>
		public static Html.HtmlAttr SseSwapMode(Swap swap)
			=> Html.Attr(Attrs.SseSwap, SwapToString(swap));

		/// <summary>
		/// Filters SSE messages by event name.
		/// </summary>
		/// <param name="eventName">The SSE event name to listen for.</param>
		/// <returns>An HTML attribute representing the SSE event filter.</returns>
		public static Html.HtmlAttr SseEvent(string eventName)
			=> Html.Attr(Attrs.SseEvent, eventName);

		/// <summary>
		/// Disables SSE handling for the element.
		/// </summary>
		/// <param name="on">
		/// <see langword="true" /> to emit the SSE disable attribute; otherwise, <see langword="false" />.
		/// </param>
		/// <returns>An HTML attribute representing the SSE disable behavior.</returns>
		public static Html.HtmlAttr SseDisable(bool on = true)
			=> Html.Bool(Attrs.SseDisable, on);

		/// <summary>
		/// Creates an out-of-band invocation element.
		/// </summary>
		/// <param name="targetSelector">The CSS selector identifying the invocation target.</param>
		/// <param name="swap">The swap behavior to apply to the invocation target.</param>
		/// <param name="payload">Optional invocation payload content.</param>
		/// <param name="wrapInTemplate">
		/// <see langword="true" /> to wrap the payload in a <c>template</c> element; otherwise, <see langword="false" />.
		/// </param>
		/// <returns>An HTML content instance representing the invocation element.</returns>
		/// <exception cref="ArgumentException">
		/// <paramref name="targetSelector" /> is null, empty, or whitespace.
		/// </exception>
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

		/// <summary>
		/// Creates an abort directive that suppresses the main swap for the current response.
		/// </summary>
		/// <param name="reason">An optional traceability reason describing why the swap was aborted.</param>
		/// <returns>An HTML content instance representing the abort directive.</returns>
		public static IHtmlContent Abort(string? reason = null)
			=> string.IsNullOrWhiteSpace(reason)
				? Html.Tag("abort")
				: Html.Tag("abort", Html.Attr("reason", reason));

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