# HeimdallFramework.Functions

The Heimdall HTTP pipeline for **.NET 10 isolated-worker Azure Functions with ASP.NET Core integration**. This is a preview package; use it with the matching `HeimdallFramework.Server` preview.

Use `ConfigureFunctionsWebApplication()`, register `AddHeimdallFunctions`, and supply one application-owned HTTP trigger:

```csharp
using Heimdall.Functions;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();
builder.Services.AddHeimdallFunctions(
    configure: options => options.EnableDetailedErrors = false,
    assemblies: [typeof(MyInvocations).Assembly]);
builder.Build().Run();
```

```csharp
using Heimdall.Functions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

public sealed class HeimdallHttp(HeimdallFunctions heimdall)
{
    [Function("Heimdall")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
            Route = "__heimdall/v1/{*path}")] HttpRequest request,
        string? path) => heimdall.HandleAsync(request, path);
}
```

The catch-all value must be relative to `__heimdall/v1`. Supported paths are `content/invocations`, legacy `content/actions`, `csrf`, `bifrost/token`, and `bifrost`. Unknown paths return 404; wrong methods return 405 with `Allow`. The dispatcher awaits the full SSE lifetime and writes directly to the live HTTP response. Do not wrap it in background work or buffer the response.

For a hand-written GET trigger, call `heimdall.RenderPageAsync(request, (services, context) => Task.FromResult(MyPage.Render()))`. Use `RenderAuthorizedPageAsync` for protected pages, with an optional named policy. The single-app starter instead registers page route patterns in `Program.cs` and dispatches them through one catch-all HTTP trigger. Functions does not run ASP.NET Core routing middleware; the adapter dispatches the SDK's static web asset endpoints for RCL and app assets.

## Single-app starter

The unified `HeimdallFramework.Templates` **5.2.0-preview.2** package includes `heimdall-functions` with `--authentication none` and `--authentication oidc`. Install the locally packed template package and make matching Functions/Server previews available through a local NuGet feed:

```powershell
dotnet new heimdall-functions -n MyFunctionsUi --authentication none
dotnet new heimdall-functions -n MyPrivateFunctionsUi --authentication oidc
```

The starter uses an empty route prefix, the `HeimdallFramework.Web` RCL runtime, a counter/Bifrost example, and normal DI. `UiFunctions` is the one build-time HTTP trigger; add pages in `Program.cs` without writing a Function class per page:

```csharp
builder.Services.AddHeimdallFunctionPages(pages =>
{
    pages.MapPage("/", context => HomePage.Render(context, false));
    pages.MapPage("/orders/{id:int}", context =>
        OrdersPage.Render(context.Request.RouteValues["id"]));
    pages.MapPage("/members", context => MembersPage.Render(context.User))
        .RequireAuthorization();
});
```

Page patterns use ASP.NET Core route syntax, including parameters, constraints, optional segments, and catch-all paths. Renderers read values from `HttpContext.Request.RouteValues`; literal and constrained matches take precedence. The page builder accepts endpoint conventions such as `RequireAuthorization`, `WithName`, and `WithDisplayName`. The OIDC variant also includes validated provider settings, cookie sessions, a protected protocol, callback handling, and POST logout with antiforgery. No-auth output omits that scaffold and its OIDC dependency. One catch-all trigger has one Functions `AuthorizationLevel` for every route; use explicit triggers when pages and protocol need different host key levels. Follow the generated README and Authentication/README.md.

## Static assets

Use `Microsoft.NET.Sdk.Razor` for the Function App project so the SDK generates RCL static web asset manifests and copies assets on publish. `HeimdallFramework.Functions` references the `HeimdallFramework.Web` RCL; other referenced RCLs work through the same manifest. Load `/_content/HeimdallFramework.Web/heimdall-bundle.min.js` (or the debug bundle) in a layout. With Functions' default `api` route prefix, prepend `/api` to that URL.

`AddHeimdallFunctions` registers `HeimdallFunctionStaticWebAssets` and `HeimdallFunctionAssets`. The starter's single trigger dispatches SDK static asset routes, including `/_content/{library}/...`, plus `/assets/*` for application files. With explicit triggers, forward a GET/HEAD RCL route to the SDK asset dispatcher:

```csharp
[Function("RclAssets")]
public async Task<IActionResult> RclAssets(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", "head",
        Route = "_content/{*path}")] HttpRequest request,
    string? path) => await staticWebAssets.TryServeAsync(request, "_content/" + path)
        ?? new NotFoundResult();
```

Forward an application asset trigger separately if using `/assets/*`:

```csharp
[Function("Assets")]
public IActionResult Assets(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", "head",
        Route = "assets/{*path}")] HttpRequest request,
    string? path) => assets.Serve(request, path);
```

Inject `HeimdallFunctionStaticWebAssets staticWebAssets` and `HeimdallFunctionAssets assets` into the function class. The RCL dispatcher uses the app's SDK build manifest locally and its publish manifest after deployment. It retains the SDK's asset URLs, fingerprints, compression variants, cache headers, ETags, and range handling. `HeimdallFunctionAssetOptions.StaticWebAssetsManifestPath` can select a nonstandard endpoint manifest. The `/assets/*` handler defaults to `AppContext.BaseDirectory/wwwroot`, rejects traversal and filesystem links, serves recognized MIME types with `nosniff`, supports HEAD/ranges, and revalidates cached responses using ETags. Configure `RootPath` or `CacheMaxAge` as needed. Fingerprint application asset names before choosing a long cache duration.

The single-trigger router serves every route in the manifest, including an RCL with a custom static asset base path. With explicit Functions triggers, map that custom base path to `TryServeAsync` as well.

Rebuild the Function App after changing an RCL asset locally. The adapter serves the generated manifest as a stable snapshot instead of enabling ASP.NET Core's development-time static asset reload, which is tied to its middleware pipeline.

## Authentication helpers and renderers

`AuthorizeAsync(request, policyName)` returns null when allowed, otherwise a challenge/forbid result. Use it before `HandleAsync` to protect an entire protocol trigger. `RenderAuthorizedPageAsync` applies that gate before rendering. `ChallengeAsync` starts a configured scheme with a validated local return path; `HandleAuthenticationAsync` processes the scheme's actual callback handler; `SignOutAsync` requires POST and validates antiforgery before signing out explicit cookie/remote schemes. The extension does not register an identity provider automatically.

The adapter establishes and restores `IHttpContextAccessor` for rendering and result execution, including concurrent requests. For compiled MVC/Razor partials, use `Microsoft.NET.Sdk.Razor`, set `AddRazorSupportForMvc=true`, and register `AddHeimdallFunctionsMvc<TMarker>()` with a marker from the compiled-view assembly. This enables view discovery. `AddHeimdallBlazor()` supports static component rendering with the Razor SDK. Both renderers are exercised in the real worker; controller/Razor Pages routing and interactive Blazor circuits remain outside this adapter.

## Function keys

Choose `AuthorizationLevel.Function` on the trigger when key enforcement is required. Azure validates keys before Heimdall executes. Supply `x-functions-key` through an origin-filtered `Heimdall.config.requestHeaders` provider for content invocations, CSRF, and subscribe-token requests. Native EventSource requires a `code` query parameter in the configured Bifrost URL. Browser-visible keys are not a replacement for user authentication; a gateway can hold private keys server-side.

`npm run test:keys` starts Core Tools with `--enableAuth`, seeds a random function-scoped key in an isolated temporary key store, and verifies missing/invalid keys, header/query keys, browser invocations, and SSE reconnects. The default local host bypasses key enforcement.

## Browser routes

With Functions' default `api` route prefix, configure these after the browser bundle loads and before DOM boot:

```javascript
Object.assign(Heimdall.config.endpoints, {
  contentInvocations: '/api/__heimdall/v1/content/invocations',
  csrf: '/api/__heimdall/v1/csrf',
  bifrostToken: '/api/__heimdall/v1/bifrost/token',
  bifrost: '/api/__heimdall/v1/bifrost'
});
```

Alternatively set `extensions.http.routePrefix` to an empty string in `host.json` and use the browser defaults. Include any additional gateway path prefix in all four URLs. CORS and static frontend hosting are application responsibilities.

## Security and host boundaries

- Antiforgery is registered and remains enabled by default. Explicit per-invocation opt-outs and global configuration follow Server behavior.
- The adapter authenticates the configured default ASP.NET Core scheme before issuing identity-bound tokens or executing content invocations. Register real authentication and authorization services; explicit invocation `[Authorize]` policies/roles are evaluated by the shared Server pipeline. Existing platform-populated `HttpContext.User` is preserved when default authentication has no result.
- Use `RenderAuthorizedPageAsync` to protect pages and `AuthorizeAsync` to gate a complete trigger. `[Authorize]` on a Functions trigger and ASP.NET Core fallback policies are not an automatic page security boundary. Functions authorization levels and keys are separate from user authentication.
- Shared binding, uploads, client info, response directives, scoped DI, invocation timeout metadata, detailed-error settings, and telemetry use the existing Server implementation. Infrastructure limits can be stricter than invocation limits. `DisableRequestTimeout` cannot disable the Functions execution timeout.
- ASP.NET Core endpoint middleware is unavailable. Content invocations using `[EnableRateLimiting]` return **501** through this adapter. Enforce rate limits at the gateway or worker and remove that metadata, or host those invocations in ASP.NET Core. Other middleware such as CORS and output caching must be configured at the hosting layer.
- Persist and share ASP.NET Core Data Protection keys and the application discriminator across instances for CSRF cookies and signed subscribe tokens. Configure forwarded headers/HTTPS/cookie settings for the actual ingress.

## Bifrost

The existing stream sends and flushes the connected event, idle heartbeat comments (15 seconds by default), and published events. Disconnect cancellation removes the local subscription. The browser already reconnects with backoff and a fresh subscribe token.

The application owns its distributed backplane. Deliver messages to **each process with relevant local subscribers** and call `Bifrost.PublishAsync(topic, content, ttl)` on that process. A competing-consumer queue or Event Hub consumer group does not by itself broadcast each event to every worker. `HasSubscribers`, connection metadata, and disconnect operations are local to a process. Bifrost does not provide replay/durable delivery across reconnects.

Long-lived public SSE is **not yet verified on Azure**. Core Tools proves the local worker transport only. Validate incremental flushing, idle heartbeats, behavior past 230 seconds, configured function timeouts, scale/restart reconnects, and any gateway buffering on your selected Azure hosting plan. See [Functions hosting limits](https://learn.microsoft.com/en-us/azure/azure-functions/functions-scale#function-app-timeout-duration).

## Validation

See `Heimdall.Functions.Tests` for shared-pipeline integration coverage and `Heimdall.Functions.E2E` for a real isolated worker, browser checks, and an optional SSE soak test. No deployment is required for local tests.
