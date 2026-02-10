# HeimdallFramework.Server

**ALPHA RELEASE**

HeimdallFramework.Server provides the **ASP.NET Core server-side runtime** for the Heimdall framework.
It exposes the endpoints, middleware, and execution pipeline that power HTML-first *invocations*
consumed by `heimdall.js`.

Most apps will use **both** packages:
- **HeimdallFramework.Server** (server runtime)
- **HeimdallFramework.Web** (client `heimdall.js` static asset)

---

## Install

```bash
dotnet add package HeimdallFramework.Server --prerelease
```

(Optional) Client-side runtime:

```bash
dotnet add package HeimdallFramework.Web --prerelease
```

---

## Minimal setup (required antiforgery + page mapping)

Heimdall requires ASP.NET Core antiforgery to be configured and enabled.

```csharp
using Heimdall.Server;

var builder = WebApplication.CreateBuilder(args);

// Required (Heimdall uses antiforgery tokens for same-origin calls)
builder.Services.AddAntiforgery();

// Optional (only if you need it)
builder.Services.AddCors();

// Register Heimdall services
builder.Services.AddHeimdall(options => options.EnableDetailedErrors = true);

var app = builder.Build();

// Required (enables antiforgery validation)
app.UseAntiforgery();

// Optional
app.UseCors();

app.UseHttpsRedirection();

// Static files / static web assets
app.MapStaticAssets();
app.UseStaticFiles();

// Heimdall middleware + endpoints (v1 routes under /__heimdall)
app.UseHeimdall();

// Map HTML pages (routes -> page + layout)
app.MapHeimdallPage(settings =>
{
    settings.Pattern = "/";
    settings.PagePath = "index.html";
    settings.LayoutPath = "layouts/mainlayout.html";
    settings.LayoutPlaceholder = "{{page}}";

    // Optional: layout components (string token -> renderer)
    // settings.LayoutComponents.Add("{{MenuComponent}}", (sp, ctx) => MainLayout.RenderMenu(ctx, "/"));
});

app.Run();
```

### Mapping multiple pages (example)

```csharp
app.MapHeimdallPage(settings =>
{
    settings.Pattern = "/dashboard";
    settings.PagePath = "index.html";
    settings.LayoutPath = "layouts/mainlayout.html";
    settings.LayoutPlaceholder = "{{page}}";
});

app.MapHeimdallPage(settings =>
{
    settings.Pattern = "/settings";
    settings.PagePath = "settings.html";
    settings.LayoutPath = "layouts/mainlayout.html";
    settings.LayoutPlaceholder = "{{page}}";
});
```

---

## Endpoints (v1)

Heimdall.Server exposes the following same-origin endpoints:

- **Content actions**  
  `POST /__heimdall/v1/content/actions`  
  Executes a server action and returns HTML, optionally containing `<invocation>` directives.

- **CSRF token**  
  `GET /__heimdall/v1/csrf`  
  Returns an antiforgery token used by `heimdall.js` (cached client-side).

- **Bifrost (SSE)**  
  `GET /__heimdall/v1/bifrost?topic=...`  
  Server-Sent Events stream for pushing HTML and/or `<invocation>` directives.

- **Bifrost subscribe token**  
  `GET /__heimdall/v1/bifrost/token?topic=...`  
  Issues a short-lived subscribe token, gated by antiforgery/CSRF.

---

## What it does

- Routes and executes Heimdall content actions
- Produces HTML responses for DOM swaps
- Supports out-of-band DOM updates via `<invocation>`
- Integrates ASP.NET Core antiforgery protection
- Optional real-time updates via Bifrost (SSE)
- Maps routes to HTML pages with layout composition via `MapHeimdallPage(...)`

---

## Status

This package is currently **alpha**. Public APIs and behavior may change.

---

## License

MIT
