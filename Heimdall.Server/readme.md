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

## Diagnostics

The server runtime emits dependency-free `ActivitySource` traces and `System.Diagnostics.Metrics` instruments for content actions and Bifrost. Register `HeimdallDiagnostics.ActivitySourceName` and `HeimdallDiagnostics.MeterName` with your OpenTelemetry providers. Public activity, metric, and tag names are available as constants on `HeimdallDiagnostics`.

Diagnostics include action outcomes, durations, exceptions, cancellations, request and rendered response sizes, active SSE connections and subscribers, and Bifrost message delivery outcomes. Action payloads, user identities, and Bifrost topic names are not included.

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

## Links

- [Documentation](https://heimdall-framework.org)
- [Source and issue tracker](https://github.com/bradleyables22/Heimdall)
- [NuGet package](https://www.nuget.org/packages/HeimdallFramework.Server)
- [MIT license](https://github.com/bradleyables22/Heimdall/blob/master/LICENSE)
