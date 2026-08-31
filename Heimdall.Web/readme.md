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

## Asynchronous request headers

Set `Heimdall.config.requestHeaders` when authentication or another integration must resolve headers asynchronously. The provider runs when a request attempt actually begins, so coordinated or queued actions do not capture stale tokens:

```js
Heimdall.config.requestHeaders = async context => {
    const token = await auth.getAccessToken(context.signal);
    if (!token)
        throw new Error("Authentication is required.");

    return {
        Authorization: `Bearer ${token}`
    };
};
```

The context includes `kind`, `url`, `method`, `actionId`, `topic`, `requestId`, `attempt`, `sourceElement`, `signal`, and the headers Heimdall has prepared so far. `kind` is `content-action`, `csrf-token`, or `bifrost-token`; action-only values are `null` for internal token requests, including the abort signal. The provider can mutate `context.headers`, return a `Headers` instance, header pairs, a plain object, or return nothing. Returned headers override mutations with the same case-insensitive name. Explicit headers supplied to `Heimdall.invoke` or `heimdall:request-config` override provider headers, and the synchronous `heimdall:request-before` event remains the final mutation point.

The provider also runs for CSRF-token and Bifrost subscribe-token fetches. It cannot add headers to the native `EventSource` stream; authenticate the Bifrost token-minting request instead. Because Heimdall endpoints are configurable, inspect `context.kind` and `context.url` before attaching credentials that must only reach trusted origins.

A provider rejection fails closed. A content action is not sent and resolves with `ok: false`, status `0`, and code `request-headers-failed`; `heimdall:error` reports the `request-headers` phase and normal finalization still occurs. Returning `{}`, `null`, or `undefined` deliberately continues without additional headers. A pending content-action provider stops blocking Heimdall when the action is replaced, externally aborted, or times out, although providers should still honor `context.signal` to cancel their own underlying work.

The `heimdall:unauthorized` event is emitted for raw `401 Unauthorized` responses from content actions, CSRF-token requests, and Bifrost token requests. It is cancellable and exposes the request kind and identifiers, URL, method, status, response body, optional normalized redirect URL, and the browser `Response`. Applications can navigate or display login UI:

```js
document.addEventListener("heimdall:unauthorized", event => {
    event.preventDefault(); // Suppress Heimdall's Location-based redirect, if present.
    window.location.assign(`/sign-in?returnUrl=${encodeURIComponent(location.href)}`);
});
```

Preventing the event's default does not turn the failed request into a success or suppress normal error reporting. It only prevents Heimdall's automatic redirect for a `Location` challenge. The event is intentionally limited to `401`; a `403 Forbidden` response represents an authenticated caller without permission. Fetch-followed cookie login redirects continue through the existing `heimdall:redirect` or `heimdall:sse-redirect` behavior.

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
