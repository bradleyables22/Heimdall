# HeimdallFramework.Rendering

`HeimdallFramework.Rendering` provides the server-side HTML rendering foundation for [Heimdall](https://heimdall-framework.org).

It contains the lower-level `Html` helpers, the `FluentHtml` builder API, synchronous and asynchronous HTML composition, and `ToHtmlString()` / `ToHtmlStringAsync()` materialization helpers. It does not include Heimdall middleware, content actions, pages, Bifrost, or the browser runtime.

## Install

```bash
dotnet add package HeimdallFramework.Rendering
```

Applications using `HeimdallFramework.Server` receive this package transitively. Install it directly when you only need server-side HTML composition.

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

Text is encoded by default. Use `Raw(...)` only for trusted markup.

> [heimdall-framework.org](https://heimdall-framework.org) is the source of truth for setup, patterns, APIs, and security guidance.

## Related packages

- `HeimdallFramework.Server` — ASP.NET Core middleware, pages, actions, Bifrost, and static-site generation.
- `HeimdallFramework.Web` — the browser runtime distributed as Razor Class Library assets.
- `HeimdallFramework.Bootstrap` — optional strongly typed Bootstrap class helpers.

- [NuGet package](https://www.nuget.org/packages/HeimdallFramework.Rendering)
