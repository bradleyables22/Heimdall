# HeimdallFramework.Web

`HeimdallFramework.Web` distributes the [Heimdall](https://heimdall-framework.org) browser runtime as Razor Class Library static assets.

This package is the client-side half of Heimdall. It applies server-rendered updates in the browser and contains no ASP.NET Core middleware or server action pipeline. Interactive applications normally pair it with `HeimdallFramework.Server`.

> [heimdall-framework.org](https://heimdall-framework.org) is the source of truth for installation, markup, browser behavior, configuration, events, security, and compatibility. This package README intentionally avoids maintaining a second copy of the runtime documentation.

## Install

```bash
dotnet add package HeimdallFramework.Web
```

Use the package through its Razor Class Library asset. See [the current documentation](https://heimdall-framework.org) for the supported asset reference and application setup.

Forms containing file inputs are submitted as `multipart/form-data`; other declarative form payloads remain JSON. `Heimdall.invoke` also accepts a browser `FormData` instance for programmatic uploads.

Antiforgery token requests and headers are enabled by default. Applications whose Heimdall server has antiforgery validation disabled can turn off the corresponding browser work before actions or SSE subscriptions start:

```js
Heimdall.config.antiforgery = false;
```

With this setting disabled, content actions omit the verification header and Bifrost subscribe-token requests still occur but omit CSRF acquisition and the CSRF header. Antiforgery-specific retries are also disabled. The browser setting does not weaken server validation by itself; configure the server policy separately and keep both sides aligned.

Content actions can optionally include a compact browser-capability snapshot for binding to the Server package's `HeimdallClientInfo` parameter:

```js
Heimdall.config.clientInfo = true;
Heimdall.config.clientInfoMaxAgeMs = 60_000;
```

Collection is disabled by default. When enabled, the runtime collects lazily on the first action, caches the serialized snapshot, and invalidates it after resize/orientation, language, online/offline, color/motion/contrast, forced-color, pointer, or hover changes. The maximum age refreshes timezone and UTC-offset values even when the browser emits no event; set it to `0` to collect on every action.

No additional network request is made. The fixed schema is normally only a few hundred bytes and is sent as `X-Heimdall-Client-Info` on content actions, including JSON and multipart requests. It deliberately excludes user-agent strings, device model, hardware capacity, network type, and geolocation. The header is not sent on Bifrost connections. Cross-origin applications must allow the header through CORS.

The cancellable `heimdall:client-info-before` event runs for every enabled action attempt immediately before the snapshot is serialized. Its `event.detail.info` object can be mutated or replaced for that request; `actionId`, `requestId`, `attempt`, and `sourceElement` identify the action. Calling `preventDefault()` or assigning `null` to `event.detail.info` omits the header without cancelling the action. Mutations are request-local and never change the cached browser snapshot.

```js
document.addEventListener("heimdall:client-info-before", event => {
    event.detail.info.locale = getApplicationLocale();

    if (event.detail.actionId === "telemetry.ignore")
        event.preventDefault();
});
```

The existing cancellable `heimdall:request-before` event runs afterward and exposes the final serialized value through `event.detail.request.headers["X-Heimdall-Client-Info"]`. It can replace or delete the raw header, or cancel the complete action request.

Elements rendered by the Server package's fluent `.LocalizeTime(...)` helper are converted to the browser user's local timezone entirely on the client. Initial document content is processed during startup; content returned by actions, out-of-band invocations, and Bifrost is formatted before insertion. Dynamically added elements and changes to `heimdall-time`, `heimdall-time-format`, or inherited `lang` values are handled by the runtime observer.

Applications can customize or observe formatting through the cancellable `heimdall:time-before` event and the `heimdall:time-after` and `heimdall:time-error` events. Formatted output is always assigned as text, and formatting failures preserve the server fallback.

## Links

- [Documentation](https://heimdall-framework.org)
- [Source and issue tracker](https://github.com/bradleyables22/Heimdall)
- [NuGet package](https://www.nuget.org/packages/HeimdallFramework.Web)
- [MIT license](https://github.com/bradleyables22/Heimdall/blob/master/LICENSE)
