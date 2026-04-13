using System.Text.Json;
using Microsoft.AspNetCore.Html;

namespace Heimdall.Server.Rendering
{
    /// <summary>
    /// Provides fluent extension helpers for applying Heimdall attributes to FluentHtml builders.
    /// </summary>
    /// <remarks>
    /// This class offers an ergonomic wrapper over <see cref="HeimdallHtml"/> so Heimdall behaviors
    /// can be attached to elements and fragments using a fluent, strongly-typed API.
    /// </remarks>
    public static class FluentHeimdall
    {
        /// <summary>
        /// Creates a Heimdall wrapper for an element builder.
        /// </summary>
        /// <param name="b">The element builder to wrap.</param>
        /// <returns>A fluent Heimdall builder for the provided element.</returns>
        public static HeimdallBuilder Heimdall(this FluentHtml.ElementBuilder b)
            => new(b);

        /// <summary>
        /// Creates a Heimdall wrapper for a fragment builder.
        /// </summary>
        /// <param name="f">The fragment builder to wrap.</param>
        /// <returns>A fluent Heimdall fragment builder for the provided fragment.</returns>
        public static HeimdallFragmentBuilder Heimdall(this FluentHtml.FragmentBuilder f)
            => new(f);

        /// <summary>
        /// Represents a fluent wrapper that applies Heimdall attributes to an element builder.
        /// </summary>
        public readonly struct HeimdallBuilder
        {
            private readonly FluentHtml.ElementBuilder _b;

            /// <summary>
            /// Initializes a new instance of the <see cref="HeimdallBuilder"/> struct.
            /// </summary>
            /// <param name="builder">The element builder to wrap.</param>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
            public HeimdallBuilder(FluentHtml.ElementBuilder builder)
                => _b = builder ?? throw new ArgumentNullException(nameof(builder));

            /// <summary>
            /// Adds the specified Heimdall trigger and action to the element.
            /// </summary>
            /// <param name="trigger">The trigger that activates the action.</param>
            /// <param name="action">The action identifier to emit.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder On(HeimdallHtml.Trigger trigger, HeimdallHtml.ActionId action)
            {
                _b.Add(HeimdallHtml.On(trigger, action));
                return this;
            }

            /// <summary>
            /// Adds a load trigger to the element.
            /// </summary>
            /// <param name="action">The action identifier to emit.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder OnLoad(HeimdallHtml.ActionId action) { _b.Add(HeimdallHtml.OnLoad(action)); return this; }

            /// <summary>
            /// Adds a click trigger to the element.
            /// </summary>
            /// <param name="action">The action identifier to emit.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder OnClick(HeimdallHtml.ActionId action) { _b.Add(HeimdallHtml.OnClick(action)); return this; }

            /// <summary>
            /// Adds a change trigger to the element.
            /// </summary>
            /// <param name="action">The action identifier to emit.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder OnChange(HeimdallHtml.ActionId action) { _b.Add(HeimdallHtml.OnChange(action)); return this; }

            /// <summary>
            /// Adds an input trigger to the element.
            /// </summary>
            /// <param name="action">The action identifier to emit.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder OnInput(HeimdallHtml.ActionId action) { _b.Add(HeimdallHtml.OnInput(action)); return this; }

            /// <summary>
            /// Adds a submit trigger to the element.
            /// </summary>
            /// <param name="action">The action identifier to emit.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder OnSubmit(HeimdallHtml.ActionId action) { _b.Add(HeimdallHtml.OnSubmit(action)); return this; }

            /// <summary>
            /// Adds a keydown trigger to the element.
            /// </summary>
            /// <param name="action">The action identifier to emit.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder OnKeyDown(HeimdallHtml.ActionId action) { _b.Add(HeimdallHtml.OnKeyDown(action)); return this; }

            /// <summary>
            /// Adds a blur trigger to the element.
            /// </summary>
            /// <param name="action">The action identifier to emit.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder OnBlur(HeimdallHtml.ActionId action) { _b.Add(HeimdallHtml.OnBlur(action)); return this; }

            /// <summary>
            /// Adds a hover trigger to the element.
            /// </summary>
            /// <param name="action">The action identifier to emit.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder OnHover(HeimdallHtml.ActionId action) { _b.Add(HeimdallHtml.OnHover(action)); return this; }

            /// <summary>
            /// Adds a visible trigger to the element.
            /// </summary>
            /// <param name="action">The action identifier to emit.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder OnVisible(HeimdallHtml.ActionId action) { _b.Add(HeimdallHtml.OnVisible(action)); return this; }

            /// <summary>
            /// Adds a scroll trigger to the element.
            /// </summary>
            /// <param name="action">The action identifier to emit.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder OnScroll(HeimdallHtml.ActionId action) { _b.Add(HeimdallHtml.OnScroll(action)); return this; }

            /// <summary>
            /// Adds a load trigger to the element.
            /// </summary>
            /// <param name="action">The action identifier to emit.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder Load(HeimdallHtml.ActionId action) => OnLoad(action);

            /// <summary>
            /// Adds a click trigger to the element.
            /// </summary>
            /// <param name="action">The action identifier to emit.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder Click(HeimdallHtml.ActionId action) => OnClick(action);

            /// <summary>
            /// Adds a change trigger to the element.
            /// </summary>
            /// <param name="action">The action identifier to emit.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder Change(HeimdallHtml.ActionId action) => OnChange(action);

            /// <summary>
            /// Adds an input trigger to the element.
            /// </summary>
            /// <param name="action">The action identifier to emit.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder Input(HeimdallHtml.ActionId action) => OnInput(action);

            /// <summary>
            /// Adds a submit trigger to the element.
            /// </summary>
            /// <param name="action">The action identifier to emit.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder Submit(HeimdallHtml.ActionId action) => OnSubmit(action);

            /// <summary>
            /// Adds a keydown trigger to the element.
            /// </summary>
            /// <param name="action">The action identifier to emit.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder KeyDown(HeimdallHtml.ActionId action) => OnKeyDown(action);

            /// <summary>
            /// Adds a blur trigger to the element.
            /// </summary>
            /// <param name="action">The action identifier to emit.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder Blur(HeimdallHtml.ActionId action) => OnBlur(action);

            /// <summary>
            /// Adds a hover trigger to the element.
            /// </summary>
            /// <param name="action">The action identifier to emit.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder Hover(HeimdallHtml.ActionId action) => OnHover(action);

            /// <summary>
            /// Adds a visible trigger to the element.
            /// </summary>
            /// <param name="action">The action identifier to emit.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder Visible(HeimdallHtml.ActionId action) => OnVisible(action);

            /// <summary>
            /// Adds a scroll trigger to the element.
            /// </summary>
            /// <param name="action">The action identifier to emit.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder Scroll(HeimdallHtml.ActionId action) => OnScroll(action);

            /// <summary>
            /// Sets the DOM target that will receive the response content.
            /// </summary>
            /// <param name="selector">The selector for the target element.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder Target(string selector) { _b.Add(HeimdallHtml.Target(selector)); return this; }

            /// <summary>
            /// Sets the swap behavior used when applying returned content.
            /// </summary>
            /// <param name="swap">The swap mode to apply.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder Swap(HeimdallHtml.Swap swap) { _b.Add(HeimdallHtml.SwapMode(swap)); return this; }

            /// <summary>
            /// Disables the element while a request is in progress.
            /// </summary>
            /// <param name="on"><see langword="true"/> to emit the attribute; otherwise, <see langword="false"/>.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder Disable(bool on = true) { _b.Add(HeimdallHtml.Disable(on)); return this; }

            /// <summary>
            /// Prevents the browser's default behavior for the element.
            /// </summary>
            /// <param name="on"><see langword="true"/> to emit the attribute; otherwise, <see langword="false"/>.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder PreventDefault(bool on = true) { _b.Add(HeimdallHtml.PreventDefault(on)); return this; }

            /// <summary>
            /// Prevents Heimdall from resolving triggers past this element for the specified trigger names.
            /// </summary>
            /// <param name="triggers">One or more trigger names to ignore.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder Ignore(params string[] triggers)
            {
                _b.Add(HeimdallHtml.Ignore(triggers));
                return this;
            }

            /// <summary>
            /// Prevents Heimdall from resolving triggers past this element for the specified trigger types.
            /// </summary>
            /// <param name="triggers">One or more trigger types to ignore.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder Ignore(params HeimdallHtml.Trigger[] triggers)
            {
                _b.Add(HeimdallHtml.Ignore(triggers));
                return this;
            }

            /// <summary>
            /// Prevents Heimdall from resolving all trigger types past this element.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder IgnoreAll()
            {
                _b.Add(HeimdallHtml.Ignore(HeimdallHtml.IgnoreTriggers.All));
                return this;
            }

            /// <summary>
            /// Controls how Heimdall matches an actionable element when the trigger fires.
            /// </summary>
            /// <param name="scope">The scope behavior to apply.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder Scope(HeimdallHtml.Scope scope)
            {
                _b.Add(HeimdallHtml.ScopeMode(scope));
                return this;
            }

            /// <summary>
            /// Resolves the nearest eligible ancestor that declares the trigger attribute.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder ScopeClosest() => Scope(HeimdallHtml.Scope.Closest);

            /// <summary>
            /// Only resolves the element when it is the direct event target.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder ScopeSelf() => Scope(HeimdallHtml.Scope.Self);

            /// <summary>
            /// Sets the swap mode to inner.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder SwapInner() => Swap(HeimdallHtml.Swap.Inner);

            /// <summary>
            /// Sets the swap mode to outer.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder SwapOuter() => Swap(HeimdallHtml.Swap.Outer);

            /// <summary>
            /// Sets the swap mode to beforeend.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder SwapBeforeEnd() => Swap(HeimdallHtml.Swap.BeforeEnd);

            /// <summary>
            /// Sets the swap mode to afterbegin.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder SwapAfterBegin() => Swap(HeimdallHtml.Swap.AfterBegin);

            /// <summary>
            /// Sets the swap mode to none.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder SwapNone() => Swap(HeimdallHtml.Swap.None);

            /// <summary>
            /// Serializes and attaches a JSON payload to the element.
            /// </summary>
            /// <param name="payload">The payload object to serialize.</param>
            /// <param name="options">Optional JSON serialization settings.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder Payload(object? payload, JsonSerializerOptions? options = null)
            {
                _b.Add(HeimdallHtml.Payload(payload, options));
                return this;
            }

            /// <summary>
            /// Forces an empty JSON payload.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder PayloadEmptyObject()
            {
                _b.Add(HeimdallHtml.PayloadEmptyObject());
                return this;
            }

            /// <summary>
            /// Sets the payload source directive.
            /// </summary>
            /// <param name="from">The payload source expression.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder PayloadFrom(string from)
            {
                _b.Add(HeimdallHtml.PayloadFromDirective(from));
                return this;
            }

            /// <summary>
            /// Uses the closest enclosing form as the payload source.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder PayloadFromClosestForm()
                => PayloadFrom(HeimdallHtml.PayloadFrom.ClosestForm);

            /// <summary>
            /// Uses the current element as the payload source.
            /// </summary>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder PayloadFromSelf()
                => PayloadFrom(HeimdallHtml.PayloadFrom.Self);

            /// <summary>
            /// Uses the nearest Heimdall state container as the payload source.
            /// </summary>
            /// <param name="key">An optional keyed state name.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder PayloadFromClosestState(string? key = null)
            {
                _b.Add(HeimdallHtml.PayloadFromClosestState(key));
                return this;
            }

            /// <summary>
            /// References a global payload object by path.
            /// </summary>
            /// <param name="globalPath">The global object path.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder PayloadRef(string globalPath)
            {
                _b.Add(HeimdallHtml.PayloadRef(globalPath));
                return this;
            }

            /// <summary>
            /// Uses a global reference path as the payload source.
            /// </summary>
            /// <param name="globalPath">The global object path.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder PayloadFromRef(string globalPath)
                => PayloadFrom(HeimdallHtml.PayloadFrom.Ref(globalPath));

            /// <summary>
            /// Writes an unkeyed state payload to the element.
            /// </summary>
            /// <param name="state">The state object to serialize.</param>
            /// <param name="options">Optional JSON serialization settings.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder State(object? state, JsonSerializerOptions? options = null)
            {
                _b.Add(HeimdallHtml.State(state, options));
                return this;
            }

            /// <summary>
            /// Writes a keyed state payload to the element.
            /// </summary>
            /// <param name="key">The state key.</param>
            /// <param name="state">The state object to serialize.</param>
            /// <param name="options">Optional JSON serialization settings.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder State(string key, object? state, JsonSerializerOptions? options = null)
            {
                _b.Add(HeimdallHtml.State(key, state, options));
                return this;
            }

            /// <summary>
            /// Writes raw JSON state to the element.
            /// </summary>
            /// <param name="json">The JSON value to write.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder StateJson(string json)
            {
                _b.Add(HeimdallHtml.StateJson(json));
                return this;
            }

            /// <summary>
            /// Writes raw keyed JSON state to the element.
            /// </summary>
            /// <param name="key">The state key.</param>
            /// <param name="json">The JSON value to write.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder StateJson(string key, string json)
            {
                _b.Add(HeimdallHtml.StateJson(key, json));
                return this;
            }

            /// <summary>
            /// Adds a debounce delay to the trigger.
            /// </summary>
            /// <param name="ms">The debounce duration in milliseconds.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder DebounceMs(int ms) { _b.Add(HeimdallHtml.DebounceMs(ms)); return this; }

            /// <summary>
            /// Filters key-based triggers using the supplied key specification.
            /// </summary>
            /// <param name="keySpec">The key filter expression.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder Key(string keySpec) { _b.Add(HeimdallHtml.Key(keySpec)); return this; }

            /// <summary>
            /// Adds a delay before hover triggers are activated.
            /// </summary>
            /// <param name="ms">The delay in milliseconds.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder HoverDelayMs(int ms) { _b.Add(HeimdallHtml.HoverDelayMs(ms)); return this; }

            /// <summary>
            /// Ensures a visible trigger runs only once.
            /// </summary>
            /// <param name="on"><see langword="true"/> to emit the attribute; otherwise, <see langword="false"/>.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder VisibleOnce(bool on = true) { _b.Add(HeimdallHtml.VisibleOnce(on)); return this; }

            /// <summary>
            /// Sets the scroll trigger threshold.
            /// </summary>
            /// <param name="px">The threshold in pixels.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder ScrollThresholdPx(int px) { _b.Add(HeimdallHtml.ScrollThresholdPx(px)); return this; }

            /// <summary>
            /// Configures a polling interval for repeated execution.
            /// </summary>
            /// <param name="ms">The polling interval in milliseconds.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder PollMs(int ms) { _b.Add(HeimdallHtml.PollMs(ms)); return this; }

            /// <summary>
            /// Enables SSE updates for the specified topic.
            /// </summary>
            /// <param name="topic">The SSE topic to subscribe to.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder SseTopic(string topic) { _b.Add(HeimdallHtml.SseTopic(topic)); return this; }

            /// <summary>
            /// Enables SSE updates using the alternate topic attribute.
            /// </summary>
            /// <param name="topic">The SSE topic to subscribe to.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder SseTopicAlias(string topic) { _b.Add(HeimdallHtml.SseTopicAlias(topic)); return this; }

            /// <summary>
            /// Sets the target for SSE content updates.
            /// </summary>
            /// <param name="selector">The selector for the target element.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder SseTarget(string selector) { _b.Add(HeimdallHtml.SseTarget(selector)); return this; }

            /// <summary>
            /// Sets the swap mode used for SSE updates.
            /// </summary>
            /// <param name="swap">The SSE swap mode.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder SseSwap(HeimdallHtml.Swap swap) { _b.Add(HeimdallHtml.SseSwapMode(swap)); return this; }

            /// <summary>
            /// Filters SSE updates to a specific event name.
            /// </summary>
            /// <param name="eventName">The SSE event name.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder SseEvent(string eventName) { _b.Add(HeimdallHtml.SseEvent(eventName)); return this; }

            /// <summary>
            /// Disables SSE behavior for the element.
            /// </summary>
            /// <param name="on"><see langword="true"/> to emit the attribute; otherwise, <see langword="false"/>.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder SseDisable(bool on = true) { _b.Add(HeimdallHtml.SseDisable(on)); return this; }

            /// <summary>
            /// Configures a complete SSE binding on the element.
            /// </summary>
            /// <param name="topic">The SSE topic to subscribe to.</param>
            /// <param name="targetSelector">The selector that receives SSE updates.</param>
            /// <param name="swap">The swap mode used when applying SSE content.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallBuilder Sse(string topic, string targetSelector, HeimdallHtml.Swap swap = HeimdallHtml.Swap.BeforeEnd)
            {
                _b.Add(
                    HeimdallHtml.SseTopic(topic),
                    HeimdallHtml.SseTarget(targetSelector),
                    HeimdallHtml.SseSwapMode(swap)
                );
                return this;
            }
        }

        /// <summary>
        /// Represents a fluent wrapper that applies Heimdall content to a fragment builder.
        /// </summary>
        public readonly struct HeimdallFragmentBuilder
        {
            private readonly FluentHtml.FragmentBuilder _f;

            /// <summary>
            /// Initializes a new instance of the <see cref="HeimdallFragmentBuilder"/> struct.
            /// </summary>
            /// <param name="fragment">The fragment builder to wrap.</param>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="fragment"/> is <see langword="null"/>.</exception>
            public HeimdallFragmentBuilder(FluentHtml.FragmentBuilder fragment)
                => _f = fragment ?? throw new ArgumentNullException(nameof(fragment));

            /// <summary>
            /// Adds an out-of-band invocation element to the fragment.
            /// </summary>
            /// <param name="targetSelector">The selector that should receive the invocation result.</param>
            /// <param name="swap">The swap mode used when applying the invocation payload.</param>
            /// <param name="payload">Optional content to include in the invocation.</param>
            /// <param name="wrapInTemplate">Determines whether the payload should be wrapped in a template element.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallFragmentBuilder Invocation(
                string targetSelector,
                HeimdallHtml.Swap swap = HeimdallHtml.Swap.Inner,
                IHtmlContent? payload = null,
                bool wrapInTemplate = false)
            {
                _f.Add(HeimdallHtml.Invocation(targetSelector, swap, payload, wrapInTemplate));
                return this;
            }

            /// <summary>
            /// Adds an abort directive to the fragment so the main swap is skipped.
            /// </summary>
            /// <param name="reason">An optional traceability reason describing why the swap was aborted.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallFragmentBuilder Abort(string? reason = null)
            {
                _f.Add(HeimdallHtml.Abort(reason));
                return this;
            }

            /// <summary>
            /// Adds a redirect directive to the fragment so the client navigates to a new URL.
            /// </summary>
            /// <param name="url">The destination URL to navigate to.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallFragmentBuilder Redirect(string url)
            {
                _f.Add(HeimdallHtml.Redirect(url));
                return this;
            }

            /// <summary>
            /// Adds HTML content directly to the fragment.
            /// </summary>
            /// <param name="content">The content to append.</param>
            /// <returns>The current builder instance.</returns>
            public HeimdallFragmentBuilder Add(IHtmlContent content)
            {
                _f.Add(content);
                return this;
            }
        }
    }
}