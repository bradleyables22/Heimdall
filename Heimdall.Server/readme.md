# HeimdallFramework.Server

**ALPHA RELEASE**

HeimdallFramework.Server provides the **ASP.NET Core server runtime** for the Heimdall framework.

It exposes middleware, endpoints, and page primitives that enable **HTML-first applications**, where the server produces documents and UI updates using hypermedia exchange instead of client-side rendering frameworks.

Most applications will use both packages:

- **HeimdallFramework.Server** → server runtime
- **HeimdallFramework.Web** → client runtime (`heimdall.js` static asset)

---

## Install

```bash
dotnet add package HeimdallFramework.Server --prerelease
```

(Optional client runtime)

```bash
dotnet add package HeimdallFramework.Web --prerelease
```

---

## Minimal setup

Heimdall requires ASP.NET Core antiforgery.

```csharp
using Heimdall.Server;

var builder = WebApplication.CreateBuilder(args);

// Required (Heimdall uses same-origin + antiforgery for actions/SSE)
builder.Services.AddAntiforgery();

// Register Heimdall services
builder.Services.AddHeimdall(options =>
{
    options.EnableDetailedErrors = true; // optional
});

var app = builder.Build();

// Required
app.UseAntiforgery();

// Static assets (needed if using HeimdallFramework.Web)
app.MapStaticAssets();
app.UseStaticFiles();

// Heimdall middleware + endpoints (v1 routes under /__heimdall)
app.UseHeimdall();

app.Run();
```

---

## Pages (core primitive)

In Heimdall, a **page is a function that returns HTML**.

Routes map directly to rendering functions — no view engine, no template requirement, no SPA hydration step.

```csharp
app.MapHeimdallPage("/", ctx =>
{
    return Html.Tag("main",
        Html.Tag("h1", "Hello Heimdall"),
        Html.Tag("p", "The browser requested a document. The server produced it.")
    );
});
```

Async example:

```csharp
app.MapHeimdallPage("/dashboard", async (sp, ctx) =>
{
    var repo = sp.GetRequiredService<IDashboardRepository>();
    var data = await repo.Get();

    return DashboardPage.Render(data);
});
```

---

## Layouts and composition

Heimdall does not impose a layout system.

Layouts are normal functions that wrap page content.

```csharp
app.MapHeimdallPage("/", ctx =>
{
    var page = HomePage.Render(ctx);
    return MainLayout.Render(page);
});
```

This keeps composition explicit, strongly-typed, and server-native.

---

## Content actions (server UI updates)

Heimdall supports server actions that return HTML fragments for DOM updates.

Client triggers → server executes → server returns HTML.

- No JSON DTO layer required
- No client rendering layer required
- HTML is the contract

Typical flow:

User interaction → heimdall.js → POST content action → server returns HTML → DOM swap

Responses may include `<invocation>` directives for out-of-band updates.

---

## Endpoints (v1)

Heimdall.Server exposes the following same-origin endpoints:

### Content actions  
`POST /__heimdall/v1/content/actions`  
Executes a server action and returns HTML (optionally containing `<invocation>` directives).

### CSRF token  
`GET /__heimdall/v1/csrf`  
Returns an antiforgery token used by `heimdall.js` (cached client-side).

### Bifrost (SSE)  
`GET /__heimdall/v1/bifrost?topic=...`  
Server-Sent Events stream for pushing HTML and/or `<invocation>` directives.

### Bifrost subscribe token  
`GET /__heimdall/v1/bifrost/token?topic=...`  
Issues a short-lived subscribe token gated by antiforgery.

---

## What this package provides

- Heimdall middleware
- Content action execution pipeline
- HTML response helpers
- Page mapping primitives (`MapHeimdallPage`)
- Antiforgery integration
- Bifrost (SSE) server runtime
- Hypermedia-driven UI execution model

---

## Design philosophy

Heimdall intentionally moves UI back toward the browser’s original model:

- The browser requests documents
- The server produces documents
- Interactions request HTML, not data
- HTML is the contract
- Composition happens on the server
- Real-time updates stream HTML

This allows rich applications without SPA complexity.

---

## Status

This package is currently **alpha**.  
Public APIs, naming, and patterns may change.

---

## License

MIT
