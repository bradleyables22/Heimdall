# HeimdallFramework.Server

HeimdallFramework.Server provides the ASP.NET Core server runtime for Heimdall: page endpoints, content action execution, antiforgery integration, Bifrost server-sent events, MVC partial rendering support, and static site generation.

Heimdall is an HTML-first framework for server-driven UI. The server renders documents and HTML fragments, and the browser applies targeted DOM updates through the Heimdall client runtime.

Most applications use:

- **HeimdallFramework.Server** - server runtime
- **HeimdallFramework.Web** - client runtime static assets
- **HeimdallFramework.Bootstrap** - optional strongly typed Bootstrap helpers

Heimdall v2 is the current released package line. v3 development is underway in this repository. Endpoint paths that include `/v1/` refer to the Heimdall wire protocol version, not the NuGet package generation.

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

Heimdall content actions and Bifrost subscribe tokens use ASP.NET Core antiforgery.

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
        Html.Tag("p", "The server produced this document.")));

app.Run();
```

Reference the client runtime from pages or layouts:

```html
<script src="/_content/HeimdallFramework.Web/heimdall-bundle.min.js"></script>
```

For debugging:

```html
<script src="/_content/HeimdallFramework.Web/heimdall-bundle.js"></script>
```

---

## Pages

In Heimdall, a page is a function that returns `IHtmlContent`.

```csharp
app.MapHeimdallPage("/", ctx =>
{
    return MainLayout.Render(HomePage.Render(), "Home");
});
```

Pages can use DI through the `(IServiceProvider, HttpContext)` overload:

```csharp
app.MapHeimdallPage("/dashboard", async (sp, ctx) =>
{
    var repo = sp.GetRequiredService<IDashboardRepository>();
    var data = await repo.GetAsync(ctx.RequestAborted);

    return MainLayout.Render(DashboardPage.Render(data), "Dashboard");
});
```

Heimdall does not impose a layout system. Layouts are normal functions that wrap page content.

---

## Static Site Generation

Heimdall can render explicitly registered pages to static HTML files.

Static generation uses the application service provider and creates a fresh DI scope for each page, so scoped services such as repositories, options, loggers, and database contexts work naturally during generation.

```csharp
builder.Services
    .AddHeimdallStaticSiteGeneration(options =>
    {
        options.OutputPath = "dist"; // default: dist, relative to content root
        options.CleanOutputPath = true; // optional: remove stale generated output before writing
        options.CopyWebRootAssets = true; // default: copy physical wwwroot assets
        options.CopyStaticWebAssets = true; // default: copy _content assets from WebRootFileProvider
        options.UsePathBase("/portal"); // optional: for subdirectory deployments
        options.UseSitemap("https://example.com");
        options.UseRobotsTxt();
    })
    .WithStaticPage("/", () =>
        MainLayout.Render(HomePage.Render(), "Home"))
    .WithStaticPage("/docs", async ctx =>
    {
        var docs = ctx.GetRequiredService<IDocumentationRepository>();
        var model = await docs.GetAllAsync(ctx.CancellationToken);

        return MainLayout.Render(DocsPage.Render(model), "Docs");
    })
    .WithNotFoundPage(() =>
        MainLayout.Render(NotFoundPage.Render(), "Not Found"));

var app = builder.Build();

app.MapStaticAssets();
app.UseDefaultFiles();
app.UseStaticFiles();

await app.RunWithHeimdallStaticSiteGenerationAsync(args);
```

Relative `OutputPath` values resolve from the application content root by default. To write directly to the ASP.NET Core web root, use the named helper instead of `"/"` or an empty string:

```csharp
builder.Services.AddHeimdallStaticSiteGeneration(options =>
{
    options.UseWebRootPath(); // writes to IWebHostEnvironment.WebRootPath
});
```

You can also target a folder beneath the web root:

```csharp
options.UseWebRootPath("static-site");
```

`UseWebRootPath()` honors custom web roots configured through ASP.NET Core. The default remains `dist` because it is the safer CI/CD artifact path and avoids mixing generated pages with source web assets.

When serving generated default documents from the ASP.NET Core app itself, add `UseDefaultFiles()` before `UseStaticFiles()`:

```csharp
app.MapStaticAssets();
app.UseDefaultFiles();
app.UseStaticFiles();
```

`UseDefaultFiles()` rewrites `/` to `/index.html`; `UseStaticFiles()` serves the rewritten file.

For sites hosted under a subdirectory, configure a public path base and use the page context when rendering rooted links:

```csharp
options.UsePathBase("/portal");

.WithStaticPage("/", ctx =>
    Layout.Render(Home.Render(), cssPath: ctx.ToSitePath("/css/site.css")))
```

`ctx.ToSitePath("/css/site.css")` returns `/css/site.css` for root deployments and `/portal/css/site.css` when `UsePathBase("/portal")` is configured. The same path base is applied to generated sitemap and default robots.txt URLs.

Use `RunWithHeimdallStaticSiteGenerationAsync(args)` as the final run call when build-time generation should be supported. If an application needs custom startup control, `UseHeimdallStaticSiteGenerationAsync(args)` is also available and returns `true` when generation ran.

For CI/CD, the server package includes opt-in MSBuild targets that run the same application command after a successful build or publish. The app still owns the command-line branch above; the target invokes it with `dotnet run --no-build --no-launch-profile -- --heimdall-generate-static`.

```xml
<PropertyGroup>
  <GenerateHeimdallStaticSiteOnBuild>true</GenerateHeimdallStaticSiteOnBuild>
</PropertyGroup>
```

Or enable it from a pipeline without changing the project file:

```bash
dotnet build -c Release -p:GenerateHeimdallStaticSiteOnBuild=true
```

For publish workflows:

```bash
dotnet publish -c Release -p:GenerateHeimdallStaticSiteOnPublish=true
```

The default build command is `--heimdall-generate-static`. The helper also accepts `--generate-static` and `generate-static` for manual runs. Override `HeimdallStaticSiteGenerationCommand`, `HeimdallStaticSiteGenerationWorkingDirectory`, `HeimdallStaticSiteGenerationTimeout`, or `HeimdallStaticSiteGenerationEnvironment` when a project needs a different entry point, working directory, timeout in milliseconds, or environment name.

When generation writes copied static web assets to `wwwroot/_content`, the build targets exclude that folder from the project `Content` and `None` item lists by default. This keeps repeat builds from treating generated RCL assets as app-authored files that collide with the original static web assets. Set `HeimdallStaticSiteExcludeGeneratedStaticWebAssets=false` only if your app intentionally owns files under `wwwroot/_content`.

Routes map to static files using common static hosting conventions:

- `/` -> `index.html`
- `/about` -> `about/index.html`
- `/docs/start` -> `docs/start/index.html`
- `/feed.xml` -> `feed.xml`

Generation validates routes, prevents path traversal, detects duplicate output files, and overwrites existing files by default. Set `OverwriteExistingFiles = false` when builds should fail instead of replacing existing output.

Heimdall writes `heimdall.static.manifest.json` by default. The manifest records generated pages, copied assets, supplemental files, byte counts, and relative output paths. Set `WriteManifest = false` to disable it.

Set `CleanOutputPath = true` to remove stale generated output before writing. Heimdall fully cleans ordinary artifact directories such as `dist`. When the output root is also the ASP.NET Core web root or content root, Heimdall uses the previous manifest instead of deleting the whole directory, so hand-authored assets are not wiped.

`UseSitemap(siteUrl)` generates `sitemap.xml` for normal HTML routes and excludes `404.html` plus non-HTML routes such as feeds. `UseRobotsTxt()` generates a permissive `robots.txt` and includes the sitemap URL when sitemap generation is enabled. Use `RobotsTxtContent` for custom robots content.

Use `WithNotFoundPage(...)` to generate `/404.html`, the common static-host fallback file.

The generator copies files from the application's physical `wwwroot` into the output root by default, preserving relative paths such as `css/app.css`, `js/site.js`, and `images/logo.png`. This keeps normal layout references working on static hosts. If the output path is inside `wwwroot`, the generator skips files already inside the output root to avoid recursively copying generated output.

The generator also copies static web assets exposed under `/_content` by `IWebHostEnvironment.WebRootFileProvider`. This is what makes Razor Class Library assets work on static hosts, including references such as:

```html
<script src="/_content/HeimdallFramework.Web/heimdall-bundle.min.js"></script>
```

The generated output preserves that path:

```text
dist/
  _content/
    HeimdallFramework.Web/
      heimdall-bundle.min.js
```

Set `CopyStaticWebAssets = false` if static web assets are handled by another deployment step.

Generating zero pages is valid. The result will contain empty page and asset lists unless web root assets were copied or supplemental files were enabled.

---

## Content Actions

Content actions are server methods that return HTML fragments for DOM updates.

Flow:

1. A rendered element declares a Heimdall trigger.
2. The Heimdall client runtime resolves payload and target information.
3. The browser posts to the content action endpoint.
4. The server invokes the action and returns HTML.
5. The client swaps the returned HTML into the target.

Example trigger:

```csharp
Html.Button(
    "Save",
    HeimdallHtml.OnClick("notes.save"),
    HeimdallHtml.Target("#notes"),
    HeimdallHtml.SwapMode(HeimdallHtml.Swap.Inner));
```

Example action:

```csharp
[ContentInvocation("notes.save")]
public static IHtmlContent Save(NotePayload payload)
{
    return NotesList.Render(payload);
}
```

Action classes can use `[ContentInvocationPrefix]` to avoid repeating a namespace:

```csharp
[ContentInvocationPrefix("orders")]
public sealed class OrderActions(IOrderRepository orders)
{
    [ContentInvocation("filter")]
    public async Task<IHtmlContent> Filter(
        [ContentPayload] OrderFilter filter,
        CancellationToken ct)
    {
        var results = await orders.SearchAsync(filter, ct);
        return OrderList.Render(results);
    }

    [ContentInvocation]
    public IHtmlContent Summary()
    {
        return OrderSummary.Render(orders.GetSummary());
    }
}
```

The resolved invocation ids are `orders.filter` and `orders.Summary`.

Content actions support:

- static methods
- instance methods activated through DI
- constructor dependencies
- `HttpContext`
- `CancellationToken`
- `ClaimsPrincipal`
- implicit service parameters
- `[FromServices]` service parameters
- one payload parameter, optionally marked with `[ContentPayload]`
- `IHtmlContent`, `Task<IHtmlContent>`, and `ValueTask<IHtmlContent>` returns

Content actions honor ASP.NET Core authorization metadata:

```csharp
using Microsoft.AspNetCore.Authorization;

[Authorize(Roles = "Admin")]
[ContentInvocation("admin.refresh")]
public static IHtmlContent RefreshAdminPanel(HttpContext ctx)
{
    return AdminPanel.Render(ctx.User);
}
```

They also honor ASP.NET Core request timeout metadata:

```csharp
using Microsoft.AspNetCore.Http.Timeouts;

[ContentInvocation("search")]
[RequestTimeout(milliseconds: 2000)]
public static async Task<IHtmlContent> Search(SearchPayload payload, CancellationToken ct)
{
    var results = await SearchService.QueryAsync(payload.Query, ct);
    return SearchResults.Render(results);
}
```

Use `[DisableRequestTimeout]` to opt out of a configured timeout.

---

## Response Directives

Responses may include directive elements that Heimdall processes before applying the main swap.

Out-of-band updates:

```csharp
return Html.Fragment(
    NotesForm.Render(),
    HeimdallHtml.Invocation(
        targetSelector: "#notes",
        swap: HeimdallHtml.Swap.Inner,
        payload: NotesList.Render(notes)));
```

Abort the main swap:

```csharp
return Html.Fragment(
    HeimdallHtml.Abort("validation-failed"),
    ErrorSummary.Render(errors));
```

Redirect:

```csharp
return HeimdallHtml.Redirect("/login");
```

Invoke a JavaScript void function:

```csharp
return Html.Fragment(
    SavedBanner.Render(),
    HeimdallHtml.JsInvokeVoid("window.App.toast.success", "Saved"));
```

JavaScript invocation paths must be explicit dotted paths rooted at `window.`, `globalThis.`, or `document.`. Bare paths such as `App.toast.success` are rejected. The default timing is after response swaps; use `JsInvokeVoidBefore(...)` when the function must run before swaps are applied.

---

## MVC Partial Rendering

MVC applications can render Razor partials from Heimdall content actions.

```csharp
builder.Services.AddHeimdallMvc();
```

`AddHeimdallMvc()` adds MVC view services, `IHttpContextAccessor`, and `IHeimdallMvcRenderer`. It does not map controller routes; configure MVC endpoints separately if the app also serves normal controllers or Razor views.

```csharp
[ContentInvocationPrefix("orders")]
public sealed class OrderActions(
    IOrderRepository orders,
    IHeimdallMvcRenderer views)
{
    [ContentInvocation("filter")]
    public async Task<IHtmlContent> Filter(OrderFilter filter, CancellationToken ct)
    {
        var results = await orders.SearchAsync(filter, ct);
        return await views.PartialAsync("_OrderList", results, ct);
    }
}
```

Partial names are resolved through the MVC view engine. Application-relative paths such as `~/Views/Orders/_OrderList.cshtml` are also supported.

---

## Bifrost SSE

Bifrost streams HTML over server-sent events.

Subscribe from rendered HTML:

```csharp
FluentHtml.Div(d =>
{
    d.Heimdall()
        .SseTopic("orders")
        .SseTarget("#orders")
        .SseSwap(HeimdallHtml.Swap.BeforeEnd);
});
```

Publish from server code:

```csharp
await bifrost.PublishAsync(
    topic: "orders",
    content: OrderToast.Render(order),
    ttl: TimeSpan.FromSeconds(10),
    ct: ct);
```

Named events are supported:

```csharp
await bifrost.PublishAsync(
    topic: "orders",
    eventName: "order.updated",
    content: OrderRow.Render(order),
    ttl: TimeSpan.FromSeconds(10),
    ct: ct);
```

Topic subscription can be authorized before a subscribe token is issued:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BifrostTopic", policy =>
        policy.RequireAuthenticatedUser());
});

builder.Services.AddHeimdall(options =>
{
    options.BifrostTopicPolicy = "BifrostTopic";
    options.AuthorizeBifrostTopic = (ctx, topic) =>
        ValueTask.FromResult(
            topic.StartsWith($"user:{ctx.User.Identity?.Name}:", StringComparison.Ordinal));
});
```

Policy handlers receive a `BifrostTopicResource` containing the topic and `HttpContext`. If both `BifrostTopicPolicy` and `AuthorizeBifrostTopic` are configured, both must allow the topic.

---

## Endpoints

Heimdall.Server exposes these same-origin endpoints. The `/v1/` route segment is the Heimdall protocol version and is independent of the NuGet package version.

- `POST /__heimdall/v1/content/actions`
- `GET /__heimdall/v1/csrf`
- `GET /__heimdall/v1/bifrost?topic=...`
- `GET /__heimdall/v1/bifrost/token?topic=...`

---

## Package Scope

This package provides:

- Heimdall service registration
- Heimdall middleware and endpoints
- `MapHeimdallPage`
- static site generation
- content action discovery and invocation
- payload binding
- authorization and timeout integration
- response directive helpers
- Bifrost SSE server runtime
- MVC partial rendering support
- strongly typed HTML and Heimdall attribute helpers

Use **HeimdallFramework.Web** for the browser runtime assets.

---

## Versioning

HeimdallFramework.Server is on the v2 package line, with v3 development underway.

The v2 APIs are intended for real application use. v3 may introduce deliberate breaking changes where they improve the framework contract, especially around static generation, JavaScript command invocation, and response orchestration. Endpoint paths remain under `/__heimdall/v1/...` until the wire protocol itself changes.

---

## License

MIT
