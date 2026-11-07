# Fluent attributes in Blazor and Razor

`HeimdallFramework.Rendering` supplies one fluent API for authoring Heimdall behavior in `.razor`
components and `.cshtml` MVC/Razor Pages views. Applications referencing `HeimdallFramework.Server`
already receive Rendering transitively.

Every method emits the canonical long-form attributes. The JavaScript shortcuts plugin is optional
and independent of these C# helpers.

## Blazor components

Add this import to `_Imports.razor`:

```razor
@using Heimdall.Server.Rendering
```

Use the fluent configuration directly with Blazor's native attribute splatting:

```razor
<button @attributes='Heimdall.OnClick(Action_Refresh).Target("#orders")'>
    Refresh
</button>

<form @attributes='Heimdall.OnSubmit(Action_Save)
                          .PayloadFromClosestForm()
                          .Target("#result")
                          .SwapInner()'>
    <input name="Name" />
    <button type="submit">Save</button>
</form>
<div id="result"></div>

@code {
    private static readonly HeimdallHtml.InvocationId Action_Refresh = "orders.refresh";
    private static readonly HeimdallHtml.InvocationId Action_Save = "orders.save";
}
```

The returned `FluentHeimdall.HeimdallBuilder` implements `IReadOnlyDictionary<string, object>`.
No `.Build()` call, component wrapper, or JavaScript configuration is needed.

Native elements receive these attributes directly. A custom Blazor component must capture unmatched
parameters and forward them to its actual HTML element:

```razor
<button @attributes="AdditionalAttributes">@ChildContent</button>

@code {
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
```

Blazor processes duplicate splatted/explicit attributes from right to left. To let an explicit
canonical markup attribute win, put it after `@attributes`:

```razor
<button @attributes='Heimdall.OnClick(Action_Refresh).Target("#default")'
        heimdall-content-target="#explicit">
    Refresh
</button>
```

Heimdall DOM swaps should target Heimdall-owned regions. Keep interactive Blazor components in
separate DOM islands so Blazor and Heimdall have clear ownership of the elements they update.

## MVC and Razor Pages

Register the import and Tag Helper once in `_ViewImports.cshtml`:

```cshtml
@using Heimdall.Server.Rendering
@addTagHelper *, Heimdall.Rendering
```

`Heimdall.Rendering` is the assembly name; the NuGet package is `HeimdallFramework.Rendering`.
Keep the application's existing MVC Tag Helper registrations alongside it.

```cshtml
<button heimdall='Heimdall.OnClick("orders.refresh").Target("#orders")'>
    Refresh
</button>

<form asp-controller="Orders" asp-action="Save" method="post"
      heimdall='Heimdall.OnSubmit("orders.save")
                        .PayloadFromClosestForm()
                        .Target("#result")
                        .SwapInner()'>
    <input asp-for="Name" />
    <button type="submit">Save</button>
</form>
```

The typed `heimdall` attribute contains a C# expression without a leading `@`. The Tag Helper applies
the generated attributes, removes `heimdall`, and preserves the element's content, normal attributes,
and other Tag Helpers' work, including MVC form actions and antiforgery fields. Explicit canonical
attributes win over the supplied configuration. A null configuration adds no attributes.

When a model's fully qualified namespace starts with `Heimdall`, use `global::Heimdall...` in its
`@model` declaration, or import its namespace and use the model's simple type name.

## Reusable configurations and precedence

Standalone configurations are immutable. Each fluent call returns a new value, so one base
configuration can be reused without accumulating changes across components or requests:

```razor
@{
    var search = Heimdall.OnInput("orders.search")
        .PayloadFromClosestForm()
        .DebounceMs(300)
        .SyncReplace();
}

<form><input name="Search" @attributes='search.Target("#orders")' /></form>
<form><input name="Search" @attributes='search.Target("#archived-orders")' /></form>
```

Repeated setters for the same attribute use the latest value, with case-insensitive attribute names.
Boolean `true` is emitted as a presence attribute; explicit `false` uses the string `"false"` so Blazor
keeps the value and the Heimdall runtime can override its default. Payloads and state are serialized
when the fluent call is made; build those parts with the current request/component's model.

Existing `FluentHtml` usage retains its attached behavior: `element.Heimdall()` updates that element,
including calls made inside its scoped callback overload.

For C# code where the root `Heimdall` namespace conflicts with the facade name, an alias inside the
consumer's namespace provides the same spelling:

```csharp
namespace MyApp.Rendering;

using Heimdall = global::Heimdall.Server.Rendering.Heimdall;

public static class OrderBehaviors
{
    public static readonly global::Heimdall.Server.Rendering.FluentHeimdall.HeimdallBuilder Refresh =
        Heimdall.OnClick("orders.refresh").Target("#orders");
}
```

## Fluent API coverage

Every existing element-level Heimdall fluent method is available as an instance chain and a static
entry point. `Heimdall.Attributes` starts an empty configuration. The same builder implementation
serves `FluentHtml`, Blazor splatting, and the Razor Tag Helper.

| Area | Methods |
| --- | --- |
| Triggers | `On`, `OnLoad`, `OnClick`, `OnChange`, `OnInput`, `OnSubmit`, `OnKeyDown`, `OnBlur`, `OnHover`, `OnVisible`, `OnDocumentVisible`, `OnOnline`, `OnScroll`, plus their existing short method aliases |
| Target and swap | `Target`, `Swap`, `SwapInner`, `SwapOuter`, `SwapBeforeEnd`, `SwapAfterBegin`, `SwapNone` |
| Event behavior | `Disable`, `PreventDefault`, `Ignore`, `IgnoreAll`, `Scope`, `ScopeClosest`, `ScopeSelf` |
| Synchronization | `Sync`, `SyncGroup`, `SyncParallel`, `SyncReplace`, `SyncDrop`, `SyncQueueLatest` |
| Payloads | `Payload`, `PayloadEmptyObject`, `PayloadFrom`, `PayloadFromClosestForm`, `PayloadFromSelf`, `PayloadFromClosestState`, `PayloadFromStorage`, `PayloadRef`, `PayloadFromRef`, `PayloadFromPlugin` |
| State | `State` and `StateJson`, with keyed and unkeyed overloads |
| Timing and filters | `DebounceMs`, `Key`, `HoverDelayMs`, `VisibleOnce`, `ScrollThresholdPx`, `PollMs` |
| Bifrost/SSE | `Sse`, `SseTopic`, `SseTopicAlias`, `SseTarget`, `SseSwap`, `SseEvent`, `SseDisable` |
| Plugins | `OnPluginTrigger`, `PayloadFromPlugin` |
| Local time | `LocalizeTime` with `DateTimeOffset` and absolute `DateTime` overloads |

## Localized time and content

Attribute splatting cannot supply child text. `LocalizeTime` carries the validated timestamp, format,
and separate `FallbackText`:

```razor
@{
    var localTime = Heimdall.LocalizeTime(Model.CreatedAt, "MMM d, yyyy HH:mm");
}
<span @attributes="localTime">@localTime.FallbackText</span>
```

In `.cshtml`, the Tag Helper fills an empty element with that encoded fallback automatically. Explicit
child content is preserved:

```cshtml
<span heimdall='Heimdall.LocalizeTime(Model.CreatedAt, "MMM d, yyyy HH:mm")'></span>
```

`Heimdall.LocalizedTime(...)` also produces a complete localizable span as `IHtmlContent`.
`DateTimeKind.Unspecified` and unsupported formats are rejected through the existing time helpers.

## Fragments and response directives

Content-producing operations return `IHtmlContent`, independently of attribute collections:

```cshtml
@Heimdall.Fragment(fragment => fragment
    .Invocation("#status", payload: FluentHtml.Span(span => span.Text("Saved")))
    .Mutate("#orders", mutation => mutation.RemoveClass("loading"))
    .HistoryPush("/orders"))
```

The complete existing fragment API is available inside `Fragment`, and as convenience methods on
`Heimdall`: `Invocation`, `Mutate`, `MutateAll`, `Abort`, `Redirect`, `History`, `HistoryPush`,
`HistoryReplace`, `SetStorage`, `RemoveStorage`, `JsInvokeVoid`, `JsInvokeVoidBefore`,
`JsInvokeVoidAfter`, `PluginCommand`, and `Add`. The callback also supports asynchronous content
preparation through `Fragment(Func<HeimdallFragmentBuilder, ValueTask>)`.

Razor views render `IHtmlContent` directly. In Blazor, adapt that typed content explicitly:

```razor
@Heimdall.Markup(Heimdall.LocalizedTime(Model.CreatedAt, "yyyy-MM-dd HH:mm"))
@Heimdall.Markup(Heimdall.Fragment(fragment => fragment
    .Add(FluentHtml.Span(span => span.Text(Model.Name)))))
```

`Markup` preserves the existing HTML encoding. `MarkupAsync` honors asynchronous HTML content and an
optional cancellation token. These adapters accept trusted typed HTML content rather than a raw string;
continue encoding user text through `Text(...)` and other existing rendering helpers.

Response directives retain their canonical protocol and execute when returned through Heimdall's
normal action/SSE response pipeline. The content adapters themselves only render HTML.
