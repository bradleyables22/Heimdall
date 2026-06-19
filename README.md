# Heimdall

Heimdall is an HTML-first framework for ASP.NET Core applications.

The core idea is simple:

```text
event -> server action -> HTML -> targeted DOM update
```

Instead of moving rendering and orchestration into a SPA, Heimdall keeps the server as the source of truth and treats HTML as the transport format. Pages render as normal documents, interactions call server content actions, and responses return HTML fragments that the browser swaps into the DOM.

Heimdall v2 is the current released package line. v3 development is underway in this repository.

Endpoint paths such as `/__heimdall/v1/content/actions` use the Heimdall wire protocol version. That `/v1/` segment is separate from the NuGet package version.

---

## Packages

### HeimdallFramework.Server

Server runtime for ASP.NET Core:

- service registration
- middleware and endpoints
- `MapHeimdallPage`
- static site generation with scoped DI, clean output, manifests, path-base support, sitemap/robots helpers, 404 pages, physical `wwwroot` copying, RCL `_content` static web asset copying, and opt-in build/publish targets for CI/CD
- content action discovery and invocation
- payload binding
- authorization and timeout integration
- response directives
- Bifrost SSE server runtime
- MVC partial rendering support
- strongly typed HTML and Heimdall attribute helpers

See [Heimdall.Server/readme.md](Heimdall.Server/readme.md).

### HeimdallFramework.Web

Browser runtime distributed as Razor Class Library static web assets:

- action invocation
- payload resolution
- DOM swaps
- out-of-band `<invocation>` handling
- abort and redirect directives
- SSE client runtime
- MutationObserver auto-boot

See [Heimdall.Web/readme.md](Heimdall.Web/readme.md).

### HeimdallFramework.Bootstrap

Strongly typed Bootstrap class helpers for server-rendered Heimdall UI.

See [Heimdall.Bootstrap/readme.md](Heimdall.Bootstrap/readme.md).

---

## Install

```bash
dotnet add package HeimdallFramework.Server
dotnet add package HeimdallFramework.Web
```

Optional Bootstrap helpers:

```bash
dotnet add package HeimdallFramework.Bootstrap
```

---

## Minimal Setup

```csharp
using Heimdall.Server;

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

Reference the browser runtime from your layout:

```html
<script src="/_content/HeimdallFramework.Web/heimdall-bundle.min.js"></script>
```

---

## Templates And Docs

- Minimal fluent template: [Heimdall-Template-App](https://github.com/bradleyables22/Heimdall-Template-App)
- Static site generation template: Heimdall-Template-Ssg in this local workspace while v3 is under development
- MVC template: [Heimdall-Mvc-Template](https://github.com/bradleyables22/Heimdall-Mvc-Template)
- Documentation site: [heimdall-framework.org](https://heimdall-framework.org)

---

## Status

Heimdall is actively evolving. v2 is the current released package line, and v3 work is focused on static site generation, JavaScript command invocation, returned-template command flows, SSE improvements, and general robustness.

---

## License

MIT
