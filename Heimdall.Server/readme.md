# HeimdallFramework.Server

`HeimdallFramework.Server` is the ASP.NET Core server runtime for [Heimdall](https://heimdall-framework.org), an HTML-first framework for server-driven applications.

The package provides the server-side building blocks for Heimdall applications, including request handling, HTML rendering, content actions, real-time updates, MVC integration, and static site generation. Interactive applications normally pair it with `HeimdallFramework.Web`, which supplies the browser runtime.

> [heimdall-framework.org](https://heimdall-framework.org) is the source of truth for installation, setup, features, APIs, configuration, security, and deployment. This package README intentionally avoids duplicating details that change as the framework evolves.

## Install

```bash
dotnet add package HeimdallFramework.Server
dotnet add package HeimdallFramework.Web
```

Optional strongly typed Bootstrap helpers are available separately:

```bash
dotnet add package HeimdallFramework.Bootstrap
```

Continue with the current guide and examples at [heimdall-framework.org](https://heimdall-framework.org).

## Bifrost subscriber hints

Background services can call `Bifrost.HasSubscribers(topic)` before performing expensive work for a live update. The result is an instantaneous view of subscribers connected to the current application instance, so it is an optimization hint rather than a delivery guarantee.

## Diagnostics

The server runtime emits dependency-free `ActivitySource` traces and `System.Diagnostics.Metrics` instruments for content actions and Bifrost. Register `HeimdallDiagnostics.ActivitySourceName` and `HeimdallDiagnostics.MeterName` with your OpenTelemetry providers. Public activity, metric, and tag names are available as constants on `HeimdallDiagnostics`.

Diagnostics include action outcomes, durations, exceptions, cancellations, request and rendered response sizes, active SSE connections and subscribers, and Bifrost message delivery outcomes. Action payloads, user identities, and Bifrost topic names are not included.

## Antiforgery configuration

Antiforgery validation is enabled by default. A specific content action—or every action on a declaring type—can opt out using ASP.NET Core's native metadata:

```csharp
using Microsoft.AspNetCore.Antiforgery;

[RequireAntiforgeryToken(false)]
[ContentInvocation("webhook.receive")]
public static IHtmlContent Receive(WebhookPayload payload)
{
    return Html.Span("Accepted");
}
```

Method metadata overrides declaring-type metadata. Disable validation globally for all Heimdall content actions and Bifrost subscribe-token requests through service configuration:

```csharp
builder.Services.AddHeimdall(options =>
{
    options.EnableAntiforgery = false;
});
```

The global switch is authoritative and defaults to `true`. When disabling it, also set `Heimdall.config.antiforgery = false` in the browser before actions or SSE subscriptions start so the runtime does not request or send CSRF tokens. Disabling antiforgery is appropriate only when another protection makes cross-site requests harmless, such as an API that does not use ambient cookie authentication.

When Heimdall antiforgery is globally disabled, Heimdall itself does not require `AddAntiforgery()` or `UseAntiforgery()`. Keep those registrations when other application endpoints still use ASP.NET Core antiforgery. Bifrost's signed subscribe tokens continue to use data protection independently.

## Client browser information

Applications can opt in to a bounded browser-capability snapshot on content-action requests. Declare `HeimdallClientInfo` like another framework-provided action parameter; it does not consume the action's payload slot:

```csharp
[ContentInvocation("dashboard.render")]
public static IHtmlContent Render(
    DashboardRequest request,
    HeimdallClientInfo client)
{
    if (!client.IsAvailable)
        return Html.Span("Browser information was not supplied.");

    return Html.Span($"{client.TimeZone}|{client.ViewportWidth}|{client.ColorScheme}");
}
```

Enable collection in the browser with `Heimdall.config.clientInfo = true`. The model includes locale/languages, IANA timezone and current UTC offset, viewport and screen dimensions, pixel ratio, orientation, color/motion/contrast preferences, forced colors, online state, and pointer/touch/hover capabilities. `DeviceCategory` is only a mobile/tablet/desktop heuristic.

When an action binds `HeimdallClientInfo`, the serialized `X-Heimdall-Client-Info` header is capped at 4096 characters; malformed or oversized values receive `400 Bad Request`. Every property is untrusted client input and must never control authorization, pricing, auditing, or other security-sensitive behavior. Cross-origin applications must allow this header in their CORS policy.

## File uploads

Content actions accept uploaded files as `IFormFile`, `IFormFileCollection`, `IFormFile[]`, or the common generic file collection interfaces. File parameters bind by parameter name and can be combined with the action's normal payload parameter:

```csharp
var form = FluentHtml.Form(form =>
{
    form.MultipartFormData();
    form.Heimdall().OnSubmit("profile.save").Target("#result");

    form.Input(Html.InputType.file, input => input
        .Name("avatar")
        .Accept("image/png", "image/jpeg")
        .Required());

    form.Button(button => button.Type("submit").Text("Upload"));
});
```

```csharp
[RequestSizeLimit(10_000_000)]
[RequestFormLimits(MultipartBodyLengthLimit = 8_000_000)]
[ContentInvocation("profile.save")]
public static IHtmlContent Save(
    [FromForm] ProfilePayload payload,
    [FromForm(Name = "avatar")] IFormFile upload)
{
    // Validate the untrusted file name and content before storing it.
    return Html.Span($"Received {upload.Length} bytes");
}
```

Forms containing file inputs are sent as `multipart/form-data` by the Heimdall browser runtime; forms without files continue to use JSON. Programmatic callers can pass a `FormData` instance to `Heimdall.invoke`.

`[FromForm]` is optional for the usual multipart payload, but makes the form-only contract explicit and prevents a registered payload type from being mistaken for a service. Its standard `Name` property aliases payload prefixes and file field names. A form-only payload rejects JSON with `415 Unsupported Media Type`.

Heimdall honors ASP.NET Core's native `[RequestSizeLimit]`, `[DisableRequestSizeLimit]`, and `[RequestFormLimits]` metadata on content-action methods and declaring types. Method metadata overrides type metadata, while the application's configured `FormOptions` remains the baseline. Limit violations return `413 Payload Too Large`. The request-size limit covers the complete encoded request; the multipart body limit applies to each multipart section, so leave room for form fields and multipart framing when setting both.

Uploads are parsed through ASP.NET Core's buffered `IFormFile` pipeline. Keep the limits finite, validate file signatures rather than trusting extensions or `Content-Type`, generate storage names instead of using `FileName`, and use a dedicated streaming endpoint for files that are too large to buffer safely.

## Browser-local time

Any fluent HTML element can render an absolute time with a server fallback and mark it for conversion to the browser user's local timezone:

```csharp
var createdAt = new DateTimeOffset(2026, 8, 26, 18, 30, 5, TimeSpan.Zero);

var content = FluentHtml.Span(span => span
    .LocalizeTime(createdAt, "MMM d, yyyy 'at' h:mm tt"));
```

`LocalizeTime` accepts `DateTimeOffset` or an absolute UTC/local `DateTime`; it rejects `DateTimeKind.Unspecified`. It emits an invariant UTC timestamp, the selected format, and encoded fallback text using `CultureInfo.CurrentCulture`. The Web runtime converts the timestamp locally without a server request and formats Heimdall response fragments before they enter the DOM.

The supported standard formats are `d`, `D`, `t`, `T`, `g`, and `G`. Custom formats support `d`, `M`, `y`, `h`, `H`, `m`, `s`, `t`, `f` (up to milliseconds), and `z`, including their normal repeated forms plus quoted and escaped literals. This is a documented C#-style subset rather than complete `DateTime.ToString` parity.

This API always means the browser's local timezone. To render a predetermined timezone, use `TimeZoneInfo.ConvertTime` and normal `.Text(...)` rendering instead.

## Links

- [Documentation](https://heimdall-framework.org)
- [Source and issue tracker](https://github.com/bradleyables22/Heimdall)
- [NuGet package](https://www.nuget.org/packages/HeimdallFramework.Server)
- [MIT license](https://github.com/bradleyables22/Heimdall/blob/master/LICENSE)
