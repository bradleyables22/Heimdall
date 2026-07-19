# Heimdall

[![Heimdall Server on NuGet](https://img.shields.io/nuget/v/HeimdallFramework.Server?label=HeimdallFramework.Server)](https://www.nuget.org/packages/HeimdallFramework.Server)
[![NuGet downloads](https://img.shields.io/nuget/dt/HeimdallFramework.Server?label=server%20downloads)](https://www.nuget.org/packages/HeimdallFramework.Server)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Heimdall is an HTML-first framework for building interactive ASP.NET Core applications without moving the UI into a single-page application.

```text
browser event -> ASP.NET action -> HTML response -> targeted DOM update
```

The server remains the source of truth. Pages render as normal documents, interactions invoke server-side content actions, and responses return HTML fragments that the browser swaps into the DOM.

**Current release:** `3.0.5` | **Target framework:** .NET 10 | **License:** MIT

The `/v1/` segment in endpoints such as `/__heimdall/v1/content/actions` identifies the browser/server wire protocol. It is independent of the NuGet package version.

## Why Heimdall

Heimdall provides a complete ASP.NET-oriented HTML-over-the-wire stack:

- server-rendered pages and HTML fragments
- content actions with dependency injection and JSON payload binding
- ASP.NET Core authorization, antiforgery, and request-timeout integration
- targeted and out-of-band DOM updates
- MVC and Razor partial rendering
- Bifrost server-sent events with authorized topic subscriptions
- static site generation with manifests, assets, sitemap, robots.txt, and path-base support
- strongly typed HTML, Heimdall attribute, and optional Bootstrap helpers
- a dependency-free browser runtime distributed as a Razor Class Library asset

Heimdall is intentionally opinionated. It is designed for ASP.NET applications that want server-owned UI and a consistent action protocol. It is not a client-side router, state manager, virtual DOM, or backend-agnostic replacement for every hypermedia library.

## Packages

| Package | Purpose |
| --- | --- |
| [`HeimdallFramework.Server`](https://www.nuget.org/packages/HeimdallFramework.Server) | ASP.NET Core middleware, pages, content actions, MVC rendering, Bifrost SSE, static generation, and HTML helpers |
| [`HeimdallFramework.Web`](https://www.nuget.org/packages/HeimdallFramework.Web) | Browser runtime and Razor Class Library static assets |
| [`HeimdallFramework.Bootstrap`](https://www.nuget.org/packages/HeimdallFramework.Bootstrap) | Optional strongly typed Bootstrap class helpers; versioned independently from the core packages |

Detailed package documentation is available in [Heimdall.Server/readme.md](Heimdall.Server/readme.md) and [Heimdall.Web/readme.md](Heimdall.Web/readme.md).

## Install

```bash
dotnet add package HeimdallFramework.Server
dotnet add package HeimdallFramework.Web
```

Optional Bootstrap helpers:

```bash
dotnet add package HeimdallFramework.Bootstrap
```

## Minimal Setup

```csharp
using Heimdall.Server;
using Heimdall.Server.Rendering;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAntiforgery();
builder.Services.AddHeimdall(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

var app = builder.Build();

app.UseAntiforgery();
app.MapStaticAssets();
app.UseStaticFiles();
app.UseHeimdall();

app.MapHeimdallPage("/", () =>
    Html.Tag("main",
        Html.Tag("h1", "Hello Heimdall"),
        Html.Tag("p", "HTML-first server-driven UI.")));

app.Run();
```

Reference the browser runtime from the page or layout:

```html
<script src="/_content/HeimdallFramework.Web/heimdall-bundle.min.js"></script>
```

## First Interaction

Declare an action on an element:

```html
<button
  heimdall-content-click="notes.refresh"
  heimdall-content-target="#notes"
  heimdall-content-swap="inner">
  Refresh
</button>

<section id="notes"></section>
```

Return the replacement HTML from ASP.NET Core:

```csharp
using Heimdall.Server;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;

[ContentInvocation("notes.refresh")]
public static IHtmlContent Refresh()
{
    return Html.Tag("p", "Rendered on the server.");
}
```

Content actions also support instance activation through dependency injection, constructor dependencies, `HttpContext`, `ClaimsPrincipal`, `CancellationToken`, service parameters, typed payloads, authorization metadata, and request-timeout metadata.

## Security Model

- Content actions and Bifrost subscribe tokens integrate with ASP.NET Core antiforgery.
- Content actions honor ASP.NET Core authorization metadata.
- Bifrost topics can be protected with an authorization policy and an application-specific topic callback.
- Script elements are stripped from fragment responses before DOM insertion.
- JavaScript response directives can call only explicit dotted paths rooted at `window`, `globalThis`, or `document`; Heimdall does not evaluate JavaScript source returned by the server.
- Detailed server errors are opt-in and should be enabled only for development.

See the [security documentation](https://heimdall-framework.org/security) for deployment guidance.

## Project Templates

Start with the template that matches the rendering model you want.

### Fluent HTML web app

```bash
dotnet new install HeimdallFramework.Templates.WebApp
dotnet new heimdall-webapp -n MyHeimdallApp
```

### ASP.NET Core MVC app

```bash
dotnet new install HeimdallFramework.Templates.MvcApp
dotnet new heimdall-mvc -n MyHeimdallMvcApp
```

### Static site generation app

```bash
dotnet new install HeimdallFramework.Templates.SsgApp
dotnet new heimdall-ssg -n MyHeimdallSite
```

Template source is available in [Heimdall-Template-App](https://github.com/bradleyables22/Heimdall-Template-App), [Heimdall-Mvc-Template](https://github.com/bradleyables22/Heimdall-Mvc-Template), and [Heimdall-Template-Ssg](https://github.com/bradleyables22/Heimdall-Template-Ssg).

## Documentation

The full guide is at [heimdall-framework.org](https://heimdall-framework.org), including pages, actions, MVC integration, forms, swaps, state, payloads, Bifrost SSE, JavaScript interop, static generation, configuration, and deployment security.

## Verification

The repository includes server integration tests, browser-runtime tests against both readable and minified bundles, package-shape checks, and full-stack Playwright tests against a real ASP.NET Core host.

Run the server suite:

```bash
dotnet test Heimdall.slnx
```

Verify the browser runtime and NuGet package shape:

```bash
cd Heimdall.Web
npm ci
npm run verify:all
```

Run the full-stack browser suite:

```bash
cd Heimdall.E2E
npm ci
npm test
```

## Versioning And Status

`HeimdallFramework.Server` and `HeimdallFramework.Web` are released together. `HeimdallFramework.Bootstrap` follows its own package version because it tracks a separate helper surface.

Heimdall is maintained and available for real application use. The framework is still young, so upgrade deliberately between major versions. Breaking changes to the browser/server contract will use a new wire-protocol version rather than silently changing the existing `/v1/` contract.

Issues, questions, and implementation feedback are welcome in the [GitHub repository](https://github.com/bradleyables22/Heimdall).

## License

Heimdall is licensed under the [MIT License](LICENSE).
