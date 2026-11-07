using Microsoft.AspNetCore.Html;

namespace Heimdall.Server.Rendering;

public static partial class Heimdall
{
    /// <summary>Builds renderable HTML using the <c>PluginCommand</c> fragment operation.</summary>
    public static IHtmlContent PluginCommand(string name, object? args = null, string? targetSelector = null, HeimdallHtml.PluginCommandTiming timing = HeimdallHtml.PluginCommandTiming.After, bool allTargets = false)
        => Fragment(fragment => fragment.PluginCommand(name, args, targetSelector, timing, allTargets));

    /// <summary>Builds renderable HTML using the <c>Invocation</c> fragment operation.</summary>
    public static IHtmlContent Invocation(string targetSelector, HeimdallHtml.Swap swap = HeimdallHtml.Swap.Inner, IHtmlContent? payload = null, bool wrapInTemplate = false)
        => Fragment(fragment => fragment.Invocation(targetSelector, swap, payload, wrapInTemplate));

    /// <summary>Builds renderable HTML using the <c>Mutate</c> fragment operation.</summary>
    public static IHtmlContent Mutate(string targetSelector, Action<FluentHeimdall.MutationBuilder> build, HeimdallHtml.MutationScope scope = default)
        => Fragment(fragment => fragment.Mutate(targetSelector, build, scope));

    /// <summary>Builds renderable HTML using the <c>MutateAll</c> fragment operation.</summary>
    public static IHtmlContent MutateAll(string targetSelector, Action<FluentHeimdall.MutationBuilder> build, HeimdallHtml.MutationScope scope = default)
        => Fragment(fragment => fragment.MutateAll(targetSelector, build, scope));

    /// <summary>Builds renderable HTML using the <c>Abort</c> fragment operation.</summary>
    public static IHtmlContent Abort(string? reason = null)
        => Fragment(fragment => fragment.Abort(reason));

    /// <summary>Builds renderable HTML using the <c>Redirect</c> fragment operation.</summary>
    public static IHtmlContent Redirect(string url)
        => Fragment(fragment => fragment.Redirect(url));

    /// <summary>Builds renderable HTML using the <c>History</c> fragment operation.</summary>
    public static IHtmlContent History(HeimdallHtml.HistoryMode mode, string url)
        => Fragment(fragment => fragment.History(mode, url));

    /// <summary>Builds renderable HTML using the <c>HistoryPush</c> fragment operation.</summary>
    public static IHtmlContent HistoryPush(string url)
        => Fragment(fragment => fragment.HistoryPush(url));

    /// <summary>Builds renderable HTML using the <c>HistoryReplace</c> fragment operation.</summary>
    public static IHtmlContent HistoryReplace(string url)
        => Fragment(fragment => fragment.HistoryReplace(url));

    /// <summary>Builds renderable HTML using the <c>SetStorage</c> fragment operation.</summary>
    public static IHtmlContent SetStorage(HeimdallHtml.StorageArea area, string key, string value)
        => Fragment(fragment => fragment.SetStorage(area, key, value));

    /// <summary>Builds renderable HTML using the <c>RemoveStorage</c> fragment operation.</summary>
    public static IHtmlContent RemoveStorage(HeimdallHtml.StorageArea area, string key)
        => Fragment(fragment => fragment.RemoveStorage(area, key));

    /// <summary>Builds renderable HTML using the <c>JsInvokeVoid</c> fragment operation.</summary>
    public static IHtmlContent JsInvokeVoid(string functionPath, params object?[] args)
        => Fragment(fragment => fragment.JsInvokeVoid(functionPath, args));

    /// <summary>Builds renderable HTML using the <c>JsInvokeVoidBefore</c> fragment operation.</summary>
    public static IHtmlContent JsInvokeVoidBefore(string functionPath, params object?[] args)
        => Fragment(fragment => fragment.JsInvokeVoidBefore(functionPath, args));

    /// <summary>Builds renderable HTML using the <c>JsInvokeVoidAfter</c> fragment operation.</summary>
    public static IHtmlContent JsInvokeVoidAfter(string functionPath, params object?[] args)
        => Fragment(fragment => fragment.JsInvokeVoidAfter(functionPath, args));

    /// <summary>Builds renderable HTML using the <c>Add</c> fragment operation.</summary>
    public static IHtmlContent Add(IHtmlContent content)
        => Fragment(fragment => fragment.Add(content));
}
