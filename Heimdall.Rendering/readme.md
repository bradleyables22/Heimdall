# HeimdallFramework.Rendering

`HeimdallFramework.Rendering` provides the standalone server-side HTML and Heimdall markup-authoring foundation for [Heimdall](https://heimdall-framework.org).

It contains the lower-level `Html` helpers, the `FluentHtml` builder API, the typed `HeimdallHtml` and `FluentHeimdall` helpers, synchronous and asynchronous HTML composition, Heimdall attributes and response directives, and `ToHtmlString()` / `ToHtmlStringAsync()` materialization helpers. It does not include Heimdall middleware, content invocations, pages, Bifrost, or the browser runtime.

## Install

```bash
dotnet add package HeimdallFramework.Rendering
```

Applications using `HeimdallFramework.Server` receive this package transitively. Install it directly when you want to author complete server-rendered Heimdall markup without the Heimdall server execution pipeline.

## FluentHtml

```csharp
using Microsoft.AspNetCore.Html;
using Heimdall.Server.Rendering;

IHtmlContent content = FluentHtml.Div(div => div
    .Class("panel")
    .Text("Server-rendered HTML."));
```

Async callback overloads use the same helper names and preserve nested asynchronous content:

```csharp
var content = await FluentHtml.Div(async div =>
{
    var label = await labels.GetLabelAsync();
    div.Text(label);

    await div.Section(async section =>
    {
        section.Text("Nested async composition.");
    });
});

string markup = await content.ToHtmlStringAsync();
```

## Heimdall markup

The typed Heimdall helpers are included in this package, so pages and fragments can be authored without referencing `HeimdallFramework.Server`:

```csharp
using Microsoft.AspNetCore.Html;
using Heimdall.Server.Rendering;

IHtmlContent content = Html.Button(
    HeimdallHtml.OnClick("users.select"),
    HeimdallHtml.Target("#details"),
    HeimdallHtml.SwapMode(HeimdallHtml.Swap.Outer),
    "Select");
```

The markup can be rendered by any ASP.NET Core application. Install `HeimdallFramework.Server` when the emitted actions need Heimdall's endpoint, binding, authorization, and response pipeline, and install `HeimdallFramework.Web` when the browser needs the Heimdall runtime.

Text is encoded by default. Use `Raw(...)` only for trusted markup.

> [heimdall-framework.org](https://heimdall-framework.org) is the source of truth for setup, patterns, APIs, and security guidance.

## Related packages

- `HeimdallFramework.Server` — ASP.NET Core middleware, pages, actions, Bifrost, and static-site generation.
- `HeimdallFramework.Web` — the browser runtime distributed as Razor Class Library assets.
- `HeimdallFramework.Bootstrap` — optional strongly typed Bootstrap class helpers.

- [NuGet package](https://www.nuget.org/packages/HeimdallFramework.Rendering)
