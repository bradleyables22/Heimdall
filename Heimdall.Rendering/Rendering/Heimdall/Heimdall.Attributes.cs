using System.Text.Json;

namespace Heimdall.Server.Rendering;

public static partial class Heimdall
{
    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.OnPluginTrigger(string, HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder OnPluginTrigger(string name, HeimdallHtml.InvocationId invocation)
        => Attributes.OnPluginTrigger(name, invocation);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.PayloadFromPlugin(string)" />
    public static FluentHeimdall.HeimdallBuilder PayloadFromPlugin(string name)
        => Attributes.PayloadFromPlugin(name);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.On(HeimdallHtml.Trigger, HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder On(HeimdallHtml.Trigger trigger, HeimdallHtml.InvocationId action)
        => Attributes.On(trigger, action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.OnLoad(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder OnLoad(HeimdallHtml.InvocationId action)
        => Attributes.OnLoad(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.OnClick(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder OnClick(HeimdallHtml.InvocationId action)
        => Attributes.OnClick(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.OnChange(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder OnChange(HeimdallHtml.InvocationId action)
        => Attributes.OnChange(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.OnInput(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder OnInput(HeimdallHtml.InvocationId action)
        => Attributes.OnInput(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.OnSubmit(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder OnSubmit(HeimdallHtml.InvocationId action)
        => Attributes.OnSubmit(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.OnKeyDown(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder OnKeyDown(HeimdallHtml.InvocationId action)
        => Attributes.OnKeyDown(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.OnBlur(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder OnBlur(HeimdallHtml.InvocationId action)
        => Attributes.OnBlur(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.OnHover(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder OnHover(HeimdallHtml.InvocationId action)
        => Attributes.OnHover(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.OnVisible(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder OnVisible(HeimdallHtml.InvocationId action)
        => Attributes.OnVisible(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.OnDocumentVisible(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder OnDocumentVisible(HeimdallHtml.InvocationId action)
        => Attributes.OnDocumentVisible(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.OnOnline(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder OnOnline(HeimdallHtml.InvocationId action)
        => Attributes.OnOnline(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.OnScroll(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder OnScroll(HeimdallHtml.InvocationId action)
        => Attributes.OnScroll(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.Load(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder Load(HeimdallHtml.InvocationId action)
        => Attributes.Load(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.Click(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder Click(HeimdallHtml.InvocationId action)
        => Attributes.Click(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.Change(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder Change(HeimdallHtml.InvocationId action)
        => Attributes.Change(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.Input(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder Input(HeimdallHtml.InvocationId action)
        => Attributes.Input(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.Submit(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder Submit(HeimdallHtml.InvocationId action)
        => Attributes.Submit(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.KeyDown(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder KeyDown(HeimdallHtml.InvocationId action)
        => Attributes.KeyDown(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.Blur(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder Blur(HeimdallHtml.InvocationId action)
        => Attributes.Blur(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.Hover(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder Hover(HeimdallHtml.InvocationId action)
        => Attributes.Hover(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.Visible(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder Visible(HeimdallHtml.InvocationId action)
        => Attributes.Visible(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.DocumentVisible(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder DocumentVisible(HeimdallHtml.InvocationId action)
        => Attributes.DocumentVisible(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.Online(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder Online(HeimdallHtml.InvocationId action)
        => Attributes.Online(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.Scroll(HeimdallHtml.InvocationId)" />
    public static FluentHeimdall.HeimdallBuilder Scroll(HeimdallHtml.InvocationId action)
        => Attributes.Scroll(action);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.Target(string)" />
    public static FluentHeimdall.HeimdallBuilder Target(string selector)
        => Attributes.Target(selector);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.Swap(HeimdallHtml.Swap)" />
    public static FluentHeimdall.HeimdallBuilder Swap(HeimdallHtml.Swap swap)
        => Attributes.Swap(swap);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.Disable(bool)" />
    public static FluentHeimdall.HeimdallBuilder Disable(bool on = true)
        => Attributes.Disable(on);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.PreventDefault(bool)" />
    public static FluentHeimdall.HeimdallBuilder PreventDefault(bool on = true)
        => Attributes.PreventDefault(on);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.Ignore(HeimdallHtml.Trigger[])" />
    public static FluentHeimdall.HeimdallBuilder Ignore(params HeimdallHtml.Trigger[] triggers)
        => Attributes.Ignore(triggers);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.Ignore(string)" />
    public static FluentHeimdall.HeimdallBuilder Ignore(string triggerList)
        => Attributes.Ignore(triggerList);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.IgnoreAll()" />
    public static FluentHeimdall.HeimdallBuilder IgnoreAll()
        => Attributes.IgnoreAll();

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.Scope(HeimdallHtml.EventScope)" />
    public static FluentHeimdall.HeimdallBuilder Scope(HeimdallHtml.EventScope scope)
        => Attributes.Scope(scope);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.ScopeClosest()" />
    public static FluentHeimdall.HeimdallBuilder ScopeClosest()
        => Attributes.ScopeClosest();

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.ScopeSelf()" />
    public static FluentHeimdall.HeimdallBuilder ScopeSelf()
        => Attributes.ScopeSelf();

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.Sync(HeimdallHtml.RequestSync)" />
    public static FluentHeimdall.HeimdallBuilder Sync(HeimdallHtml.RequestSync strategy)
        => Attributes.Sync(strategy);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.Sync(HeimdallHtml.RequestSync, string)" />
    public static FluentHeimdall.HeimdallBuilder Sync(HeimdallHtml.RequestSync strategy, string group)
        => Attributes.Sync(strategy, group);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.SyncParallel(string)" />
    public static FluentHeimdall.HeimdallBuilder SyncParallel(string? group = null)
        => Attributes.SyncParallel(group);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.SyncReplace(string)" />
    public static FluentHeimdall.HeimdallBuilder SyncReplace(string? group = null)
        => Attributes.SyncReplace(group);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.SyncDrop(string)" />
    public static FluentHeimdall.HeimdallBuilder SyncDrop(string? group = null)
        => Attributes.SyncDrop(group);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.SyncQueueLatest(string)" />
    public static FluentHeimdall.HeimdallBuilder SyncQueueLatest(string? group = null)
        => Attributes.SyncQueueLatest(group);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.SwapInner()" />
    public static FluentHeimdall.HeimdallBuilder SwapInner()
        => Attributes.SwapInner();

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.SwapOuter()" />
    public static FluentHeimdall.HeimdallBuilder SwapOuter()
        => Attributes.SwapOuter();

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.SwapBeforeEnd()" />
    public static FluentHeimdall.HeimdallBuilder SwapBeforeEnd()
        => Attributes.SwapBeforeEnd();

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.SwapAfterBegin()" />
    public static FluentHeimdall.HeimdallBuilder SwapAfterBegin()
        => Attributes.SwapAfterBegin();

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.SwapNone()" />
    public static FluentHeimdall.HeimdallBuilder SwapNone()
        => Attributes.SwapNone();

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.Payload(object, JsonSerializerOptions)" />
    public static FluentHeimdall.HeimdallBuilder Payload(object? payload, JsonSerializerOptions? options = null)
        => Attributes.Payload(payload, options);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.PayloadEmptyObject()" />
    public static FluentHeimdall.HeimdallBuilder PayloadEmptyObject()
        => Attributes.PayloadEmptyObject();

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.PayloadFrom(string)" />
    public static FluentHeimdall.HeimdallBuilder PayloadFrom(string from)
        => Attributes.PayloadFrom(from);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.PayloadFromClosestForm()" />
    public static FluentHeimdall.HeimdallBuilder PayloadFromClosestForm()
        => Attributes.PayloadFromClosestForm();

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.PayloadFromSelf()" />
    public static FluentHeimdall.HeimdallBuilder PayloadFromSelf()
        => Attributes.PayloadFromSelf();

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.PayloadFromClosestState(string)" />
    public static FluentHeimdall.HeimdallBuilder PayloadFromClosestState(string? key = null)
        => Attributes.PayloadFromClosestState(key);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.PayloadFromStorage(HeimdallHtml.StorageArea, string)" />
    public static FluentHeimdall.HeimdallBuilder PayloadFromStorage(HeimdallHtml.StorageArea area, string key)
        => Attributes.PayloadFromStorage(area, key);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.PayloadRef(string)" />
    public static FluentHeimdall.HeimdallBuilder PayloadRef(string globalPath)
        => Attributes.PayloadRef(globalPath);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.PayloadFromRef(string)" />
    public static FluentHeimdall.HeimdallBuilder PayloadFromRef(string globalPath)
        => Attributes.PayloadFromRef(globalPath);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.State(object, JsonSerializerOptions)" />
    public static FluentHeimdall.HeimdallBuilder State(object? state, JsonSerializerOptions? options = null)
        => Attributes.State(state, options);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.State(string, object, JsonSerializerOptions)" />
    public static FluentHeimdall.HeimdallBuilder State(string key, object? state, JsonSerializerOptions? options = null)
        => Attributes.State(key, state, options);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.StateJson(string)" />
    public static FluentHeimdall.HeimdallBuilder StateJson(string json)
        => Attributes.StateJson(json);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.StateJson(string, string)" />
    public static FluentHeimdall.HeimdallBuilder StateJson(string key, string json)
        => Attributes.StateJson(key, json);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.DebounceMs(int)" />
    public static FluentHeimdall.HeimdallBuilder DebounceMs(int ms)
        => Attributes.DebounceMs(ms);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.Key(string)" />
    public static FluentHeimdall.HeimdallBuilder Key(string keySpec)
        => Attributes.Key(keySpec);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.HoverDelayMs(int)" />
    public static FluentHeimdall.HeimdallBuilder HoverDelayMs(int ms)
        => Attributes.HoverDelayMs(ms);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.VisibleOnce(bool)" />
    public static FluentHeimdall.HeimdallBuilder VisibleOnce(bool on = true)
        => Attributes.VisibleOnce(on);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.ScrollThresholdPx(int)" />
    public static FluentHeimdall.HeimdallBuilder ScrollThresholdPx(int px)
        => Attributes.ScrollThresholdPx(px);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.PollMs(int)" />
    public static FluentHeimdall.HeimdallBuilder PollMs(int ms)
        => Attributes.PollMs(ms);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.SseTopic(string)" />
    public static FluentHeimdall.HeimdallBuilder SseTopic(string topic)
        => Attributes.SseTopic(topic);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.SseTopicAlias(string)" />
    public static FluentHeimdall.HeimdallBuilder SseTopicAlias(string topic)
        => Attributes.SseTopicAlias(topic);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.SseTarget(string)" />
    public static FluentHeimdall.HeimdallBuilder SseTarget(string selector)
        => Attributes.SseTarget(selector);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.SseSwap(HeimdallHtml.Swap)" />
    public static FluentHeimdall.HeimdallBuilder SseSwap(HeimdallHtml.Swap swap)
        => Attributes.SseSwap(swap);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.SseEvent(string)" />
    public static FluentHeimdall.HeimdallBuilder SseEvent(string eventName)
        => Attributes.SseEvent(eventName);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.SseDisable(bool)" />
    public static FluentHeimdall.HeimdallBuilder SseDisable(bool on = true)
        => Attributes.SseDisable(on);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.Sse(string, string, HeimdallHtml.Swap)" />
    public static FluentHeimdall.HeimdallBuilder Sse(string topic, string targetSelector, HeimdallHtml.Swap swap = HeimdallHtml.Swap.BeforeEnd)
        => Attributes.Sse(topic, targetSelector, swap);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.SyncGroup(string)" />
    public static FluentHeimdall.HeimdallBuilder SyncGroup(string group)
        => Attributes.SyncGroup(group);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.LocalizeTime(DateTimeOffset, string)" />
    public static FluentHeimdall.HeimdallBuilder LocalizeTime(DateTimeOffset value, string format = "G")
        => Attributes.LocalizeTime(value, format);

    /// <inheritdoc cref="FluentHeimdall.HeimdallBuilder.LocalizeTime(DateTime, string)" />
    public static FluentHeimdall.HeimdallBuilder LocalizeTime(DateTime value, string format = "G")
        => Attributes.LocalizeTime(value, format);
}
