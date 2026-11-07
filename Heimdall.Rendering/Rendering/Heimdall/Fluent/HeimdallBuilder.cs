using System.Text.Json;

namespace Heimdall.Server.Rendering
{
	/// <summary>
	/// Provides fluent extension helpers for applying Heimdall attributes to FluentHtml builders.
	/// </summary>
	/// <remarks>
	/// This class offers an ergonomic wrapper over <see cref="HeimdallHtml"/> so Heimdall behaviors
	/// can be attached to elements and fragments using a fluent, strongly-typed API.
	/// </remarks>
	public static partial class FluentHeimdall
	{
		/// <summary>
		/// Configures canonical Heimdall attributes for FluentHtml, Blazor, and Razor views.
		/// </summary>
		/// <remarks>
		/// Standalone configurations created through <see cref="global::Heimdall.Server.Rendering.Heimdall"/> are immutable.
		/// A wrapper constructed with an element builder continues to apply changes to that element.
		/// </remarks>
		public readonly partial struct HeimdallBuilder
		{

			/// <summary>Emits a fully qualified plugin trigger using its default attribute name.</summary>
			public HeimdallBuilder OnPluginTrigger(string name, HeimdallHtml.InvocationId invocation)
			{
				return AddAttributes(HeimdallHtml.OnPluginTrigger(name, invocation));
			}

			/// <summary>Uses a registered browser plugin payload source.</summary>
			public HeimdallBuilder PayloadFromPlugin(string name)
			{
				return AddAttributes(HeimdallHtml.PayloadFromPlugin(name));
			}

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
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder On(HeimdallHtml.Trigger trigger, HeimdallHtml.InvocationId action)
			{
				return AddAttributes(HeimdallHtml.On(trigger, action));
			}

			/// <summary>
			/// Adds a load trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder OnLoad(HeimdallHtml.InvocationId action) { return AddAttributes(HeimdallHtml.OnLoad(action)); }

			/// <summary>
			/// Adds a click trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder OnClick(HeimdallHtml.InvocationId action) { return AddAttributes(HeimdallHtml.OnClick(action)); }

			/// <summary>
			/// Adds a change trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder OnChange(HeimdallHtml.InvocationId action) { return AddAttributes(HeimdallHtml.OnChange(action)); }

			/// <summary>
			/// Adds an input trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder OnInput(HeimdallHtml.InvocationId action) { return AddAttributes(HeimdallHtml.OnInput(action)); }

			/// <summary>
			/// Adds a submit trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder OnSubmit(HeimdallHtml.InvocationId action) { return AddAttributes(HeimdallHtml.OnSubmit(action)); }

			/// <summary>
			/// Adds a keydown trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder OnKeyDown(HeimdallHtml.InvocationId action) { return AddAttributes(HeimdallHtml.OnKeyDown(action)); }

			/// <summary>
			/// Adds a blur trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder OnBlur(HeimdallHtml.InvocationId action) { return AddAttributes(HeimdallHtml.OnBlur(action)); }

			/// <summary>
			/// Adds a hover trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder OnHover(HeimdallHtml.InvocationId action) { return AddAttributes(HeimdallHtml.OnHover(action)); }

			/// <summary>
			/// Adds a visible trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder OnVisible(HeimdallHtml.InvocationId action) { return AddAttributes(HeimdallHtml.OnVisible(action)); }

			/// <summary>
			/// Adds a document-visible trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder OnDocumentVisible(HeimdallHtml.InvocationId action) { return AddAttributes(HeimdallHtml.OnDocumentVisible(action)); }

			/// <summary>
			/// Adds an online trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder OnOnline(HeimdallHtml.InvocationId action) { return AddAttributes(HeimdallHtml.OnOnline(action)); }

			/// <summary>
			/// Adds a scroll trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder OnScroll(HeimdallHtml.InvocationId action) { return AddAttributes(HeimdallHtml.OnScroll(action)); }

			/// <summary>
			/// Adds a load trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder Load(HeimdallHtml.InvocationId action) => OnLoad(action);

			/// <summary>
			/// Adds a click trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder Click(HeimdallHtml.InvocationId action) => OnClick(action);

			/// <summary>
			/// Adds a change trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder Change(HeimdallHtml.InvocationId action) => OnChange(action);

			/// <summary>
			/// Adds an input trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder Input(HeimdallHtml.InvocationId action) => OnInput(action);

			/// <summary>
			/// Adds a submit trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder Submit(HeimdallHtml.InvocationId action) => OnSubmit(action);

			/// <summary>
			/// Adds a keydown trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder KeyDown(HeimdallHtml.InvocationId action) => OnKeyDown(action);

			/// <summary>
			/// Adds a blur trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder Blur(HeimdallHtml.InvocationId action) => OnBlur(action);

			/// <summary>
			/// Adds a hover trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder Hover(HeimdallHtml.InvocationId action) => OnHover(action);

			/// <summary>
			/// Adds a visible trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder Visible(HeimdallHtml.InvocationId action) => OnVisible(action);

			/// <summary>
			/// Adds a document-visible trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder DocumentVisible(HeimdallHtml.InvocationId action) => OnDocumentVisible(action);

			/// <summary>
			/// Adds an online trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder Online(HeimdallHtml.InvocationId action) => OnOnline(action);

			/// <summary>
			/// Adds a scroll trigger to the element.
			/// </summary>
			/// <param name="action">The action identifier to emit.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder Scroll(HeimdallHtml.InvocationId action) => OnScroll(action);

			/// <summary>
			/// Sets the DOM target that will receive the response content.
			/// </summary>
			/// <param name="selector">The selector for the target element.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder Target(string selector) { return AddAttributes(HeimdallHtml.Target(selector)); }

			/// <summary>
			/// Sets the swap behavior used when applying returned content.
			/// </summary>
			/// <param name="swap">The swap mode to apply.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder Swap(HeimdallHtml.Swap swap) { return AddAttributes(HeimdallHtml.SwapMode(swap)); }

			/// <summary>
			/// Disables the element while a request is in progress.
			/// </summary>
			/// <param name="on"><see langword="true"/> to emit the attribute; otherwise, <see langword="false"/>.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder Disable(bool on = true) { return AddAttributes(HeimdallHtml.Disable(on)); }

			/// <summary>
			/// Prevents the browser's default behavior for the element.
			/// </summary>
			/// <param name="on"><see langword="true"/> to emit the attribute; otherwise, <see langword="false"/>.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder PreventDefault(bool on = true) { return AddAttributes(HeimdallHtml.PreventDefault(on)); }

			/// <summary>
			/// Prevents Heimdall delegated trigger resolution from crossing this element for the specified triggers.
			/// </summary>
			/// <param name="triggers">The trigger names to block.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder Ignore(params HeimdallHtml.Trigger[] triggers) { return AddAttributes(HeimdallHtml.Ignore(triggers)); }

			/// <summary>
			/// Emits a raw Heimdall ignore trigger list.
			/// </summary>
			/// <param name="triggerList">A whitespace-separated trigger list, or <c>*</c>.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder Ignore(string triggerList) { return AddAttributes(HeimdallHtml.Ignore(triggerList)); }

			/// <summary>
			/// Prevents Heimdall delegated trigger resolution from crossing this element for all triggers.
			/// </summary>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder IgnoreAll() { return AddAttributes(HeimdallHtml.IgnoreAll()); }

			/// <summary>
			/// Sets the delegated trigger matching scope.
			/// </summary>
			/// <param name="scope">The event scope to apply.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder Scope(HeimdallHtml.EventScope scope) { return AddAttributes(HeimdallHtml.Scope(scope)); }

			/// <summary>
			/// Allows delegated trigger resolution from descendant event targets.
			/// </summary>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder ScopeClosest() => Scope(HeimdallHtml.EventScope.Closest);

			/// <summary>
			/// Restricts the trigger to events whose target is this element.
			/// </summary>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder ScopeSelf() => Scope(HeimdallHtml.EventScope.Self);

			/// <summary>
			/// Sets the request synchronization strategy for this element.
			/// </summary>
			/// <param name="strategy">The synchronization strategy to apply.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder Sync(HeimdallHtml.RequestSync strategy)
			{
				return AddAttributes(HeimdallHtml.Sync(strategy));
			}

			/// <summary>
			/// Sets the request synchronization strategy and named synchronization group for this element.
			/// </summary>
			/// <param name="strategy">The synchronization strategy to apply.</param>
			/// <param name="group">The named synchronization group.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder Sync(HeimdallHtml.RequestSync strategy, string group)
			{
				return AddAttributes(HeimdallHtml.Sync(strategy), HeimdallHtml.SyncGroup(group));
			}

			/// <summary>
			/// Runs requests independently, optionally within a named synchronization group.
			/// </summary>
			public HeimdallBuilder SyncParallel(string? group = null)
				=> group is null ? Sync(HeimdallHtml.RequestSync.Parallel) : Sync(HeimdallHtml.RequestSync.Parallel, group);

			/// <summary>
			/// Cancels the active request before starting the newest request.
			/// </summary>
			public HeimdallBuilder SyncReplace(string? group = null)
				=> group is null ? Sync(HeimdallHtml.RequestSync.Replace) : Sync(HeimdallHtml.RequestSync.Replace, group);

			/// <summary>
			/// Ignores a new request while another request is active.
			/// </summary>
			public HeimdallBuilder SyncDrop(string? group = null)
				=> group is null ? Sync(HeimdallHtml.RequestSync.Drop) : Sync(HeimdallHtml.RequestSync.Drop, group);

			/// <summary>
			/// Keeps only the latest pending request while another request is active.
			/// </summary>
			public HeimdallBuilder SyncQueueLatest(string? group = null)
				=> group is null ? Sync(HeimdallHtml.RequestSync.QueueLatest) : Sync(HeimdallHtml.RequestSync.QueueLatest, group);

			/// <summary>
			/// Sets the swap mode to inner.
			/// </summary>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder SwapInner() => Swap(HeimdallHtml.Swap.Inner);

			/// <summary>
			/// Sets the swap mode to outer.
			/// </summary>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder SwapOuter() => Swap(HeimdallHtml.Swap.Outer);

			/// <summary>
			/// Sets the swap mode to beforeend.
			/// </summary>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder SwapBeforeEnd() => Swap(HeimdallHtml.Swap.BeforeEnd);

			/// <summary>
			/// Sets the swap mode to afterbegin.
			/// </summary>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder SwapAfterBegin() => Swap(HeimdallHtml.Swap.AfterBegin);

			/// <summary>
			/// Sets the swap mode to none.
			/// </summary>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder SwapNone() => Swap(HeimdallHtml.Swap.None);

			/// <summary>
			/// Serializes and attaches a JSON payload to the element.
			/// </summary>
			/// <param name="payload">The payload object to serialize.</param>
			/// <param name="options">Optional JSON serialization settings.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder Payload(object? payload, JsonSerializerOptions? options = null)
			{
				return AddAttributes(HeimdallHtml.Payload(payload, options));
			}

			/// <summary>
			/// Forces an empty JSON payload.
			/// </summary>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder PayloadEmptyObject()
			{
				return AddAttributes(HeimdallHtml.PayloadEmptyObject());
			}

			/// <summary>
			/// Sets the payload source directive.
			/// </summary>
			/// <param name="from">The payload source expression.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder PayloadFrom(string from)
			{
				return AddAttributes(HeimdallHtml.PayloadFromDirective(from));
			}

			/// <summary>
			/// Uses the closest enclosing form as the payload source.
			/// </summary>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder PayloadFromClosestForm()
				=> PayloadFrom(HeimdallHtml.PayloadFrom.ClosestForm);

			/// <summary>
			/// Uses the current element as the payload source.
			/// </summary>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder PayloadFromSelf()
				=> PayloadFrom(HeimdallHtml.PayloadFrom.Self);

			/// <summary>
			/// Uses the nearest Heimdall state container as the payload source.
			/// </summary>
			/// <param name="key">An optional keyed state name.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder PayloadFromClosestState(string? key = null)
			{
				return AddAttributes(HeimdallHtml.PayloadFromClosestState(key));
			}

			/// <summary>Uses one local or session storage value as the complete JSON payload source.</summary>
			public HeimdallBuilder PayloadFromStorage(HeimdallHtml.StorageArea area, string key)
			{
				return AddAttributes(HeimdallHtml.PayloadFromStorage(area, key));
			}

			/// <summary>
			/// References a global payload object by path.
			/// </summary>
			/// <param name="globalPath">The global object path.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder PayloadRef(string globalPath)
			{
				return AddAttributes(HeimdallHtml.PayloadRef(globalPath));
			}

			/// <summary>
			/// Uses a global reference path as the payload source.
			/// </summary>
			/// <param name="globalPath">The global object path.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder PayloadFromRef(string globalPath)
				=> PayloadFrom(HeimdallHtml.PayloadFrom.Ref(globalPath));

			/// <summary>
			/// Writes an unkeyed state payload to the element.
			/// </summary>
			/// <param name="state">The state object to serialize.</param>
			/// <param name="options">Optional JSON serialization settings.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder State(object? state, JsonSerializerOptions? options = null)
			{
				return AddAttributes(HeimdallHtml.State(state, options));
			}

			/// <summary>
			/// Writes a keyed state payload to the element.
			/// </summary>
			/// <param name="key">The state key.</param>
			/// <param name="state">The state object to serialize.</param>
			/// <param name="options">Optional JSON serialization settings.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder State(string key, object? state, JsonSerializerOptions? options = null)
			{
				return AddAttributes(HeimdallHtml.State(key, state, options));
			}

			/// <summary>
			/// Writes raw JSON state to the element.
			/// </summary>
			/// <param name="json">The JSON value to write.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder StateJson(string json)
			{
				return AddAttributes(HeimdallHtml.StateJson(json));
			}

			/// <summary>
			/// Writes raw keyed JSON state to the element.
			/// </summary>
			/// <param name="key">The state key.</param>
			/// <param name="json">The JSON value to write.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder StateJson(string key, string json)
			{
				return AddAttributes(HeimdallHtml.StateJson(key, json));
			}

			/// <summary>
			/// Adds a debounce delay to the trigger.
			/// </summary>
			/// <param name="ms">The debounce duration in milliseconds.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder DebounceMs(int ms) { return AddAttributes(HeimdallHtml.DebounceMs(ms)); }

			/// <summary>
			/// Filters key-based triggers using the supplied key specification.
			/// </summary>
			/// <param name="keySpec">The key filter expression.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder Key(string keySpec) { return AddAttributes(HeimdallHtml.Key(keySpec)); }

			/// <summary>
			/// Adds a delay before hover triggers are activated.
			/// </summary>
			/// <param name="ms">The delay in milliseconds.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder HoverDelayMs(int ms) { return AddAttributes(HeimdallHtml.HoverDelayMs(ms)); }

			/// <summary>
			/// Ensures a visible trigger runs only once.
			/// </summary>
			/// <param name="on"><see langword="true"/> to emit the attribute; otherwise, <see langword="false"/>.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder VisibleOnce(bool on = true) { return AddAttributes(HeimdallHtml.VisibleOnce(on)); }

			/// <summary>
			/// Sets the scroll trigger threshold.
			/// </summary>
			/// <param name="px">The threshold in pixels.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder ScrollThresholdPx(int px) { return AddAttributes(HeimdallHtml.ScrollThresholdPx(px)); }

			/// <summary>
			/// Configures a polling interval for repeated execution.
			/// </summary>
			/// <param name="ms">The polling interval in milliseconds.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder PollMs(int ms) { return AddAttributes(HeimdallHtml.PollMs(ms)); }

			/// <summary>
			/// Enables SSE updates for the specified topic.
			/// </summary>
			/// <param name="topic">The SSE topic to subscribe to.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder SseTopic(string topic) { return AddAttributes(HeimdallHtml.SseTopic(topic)); }

			/// <summary>
			/// Enables SSE updates using the alternate topic attribute.
			/// </summary>
			/// <param name="topic">The SSE topic to subscribe to.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder SseTopicAlias(string topic) { return AddAttributes(HeimdallHtml.SseTopicAlias(topic)); }

			/// <summary>
			/// Sets the target for SSE content updates.
			/// </summary>
			/// <param name="selector">The selector for the target element.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder SseTarget(string selector) { return AddAttributes(HeimdallHtml.SseTarget(selector)); }

			/// <summary>
			/// Sets the swap mode used for SSE updates.
			/// </summary>
			/// <param name="swap">The SSE swap mode.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder SseSwap(HeimdallHtml.Swap swap) { return AddAttributes(HeimdallHtml.SseSwapMode(swap)); }

			/// <summary>
			/// Filters SSE updates to a specific event name.
			/// </summary>
			/// <param name="eventName">The SSE event name.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder SseEvent(string eventName) { return AddAttributes(HeimdallHtml.SseEvent(eventName)); }

			/// <summary>
			/// Disables SSE behavior for the element.
			/// </summary>
			/// <param name="on"><see langword="true"/> to emit the attribute; otherwise, <see langword="false"/>.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder SseDisable(bool on = true) { return AddAttributes(HeimdallHtml.SseDisable(on)); }

			/// <summary>
			/// Configures a complete SSE binding on the element.
			/// </summary>
			/// <param name="topic">The SSE topic to subscribe to.</param>
			/// <param name="targetSelector">The selector that receives SSE updates.</param>
			/// <param name="swap">The swap mode used when applying SSE content.</param>
			/// <returns>The configured builder instance.</returns>
			public HeimdallBuilder Sse(string topic, string targetSelector, HeimdallHtml.Swap swap = HeimdallHtml.Swap.BeforeEnd)
			{
				return AddAttributes(
					HeimdallHtml.SseTopic(topic),
					HeimdallHtml.SseTarget(targetSelector),
					HeimdallHtml.SseSwapMode(swap)
				);
			}
		}
	}
}
