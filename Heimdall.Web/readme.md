# HeimdallFramework.Web

> **HeimdallFramework.Web** distributes the **Heimdall JavaScript runtime** as Razor Class Library (RCL) static web assets.
>
> This package contains **only the client runtime**.
>
> It does **not** include:
>
> * Server endpoints
> * Action pipeline
> * MVC / Razor helpers
> * ASP.NET middleware
>
> For the server implementation, install **HeimdallFramework.Server**.

* [Full documentation](https://heimdall-framework.org)
* [Source and issue tracker](https://github.com/bradleyables22/Heimdall)
* [NuGet package](https://www.nuget.org/packages/HeimdallFramework.Web)

---

## Overview

Heimdall is an **HTML-first hypermedia runtime for ASP.NET** that enables server-driven UI without SPA complexity.

Instead of building client apps, you:

* Render HTML on the server
* Trigger actions via attributes
* Return HTML fragments
* Let Heimdall handle DOM updates

The design goal:

> Use the browser the way it was intended — as a document renderer.

---

## What This Package Contains

* `heimdall-bundle.min.js` production runtime
* `heimdall-bundle.js` readable debugging runtime
* Static web asset delivery via Razor Class Library
* Automatic boot on DOM ready
* Declarative action system
* Payload resolution engine
* DOM swap engine
* Out-of-band update support (`<invocation>`)
* SSE runtime (“Bifrost”)
* MutationObserver auto-boot

This package intentionally contains **no server implementation**.

---

## Installation

Install:

```
dotnet add package HeimdallFramework.Web
```

Reference the minified runtime in production:

```html
<script src="/_content/HeimdallFramework.Web/heimdall-bundle.min.js"></script>
```

For debugging, use the readable generated runtime:

```html
<script src="/_content/HeimdallFramework.Web/heimdall-bundle.js"></script>
```

Heimdall boots automatically.

---

## Core Concept

HTML attributes define behavior.

```html
<button heimdall-content-click="Notes.Create">
    Save
</button>
```

Flow:

1. Payload resolved
2. POST sent
3. Server returns HTML
4. DOM swap applied

No client framework required.

---

## Triggers

Supported attributes:

* `heimdall-content-load`
* `heimdall-content-click`
* `heimdall-content-change`
* `heimdall-content-input`
* `heimdall-content-submit`
* `heimdall-content-keydown`
* `heimdall-content-blur`
* `heimdall-content-hover`
* `heimdall-content-visible`
* `heimdall-content-scroll`

Example:

```html
<div heimdall-content-visible="Feed.LoadMore"></div>
```

---

## Targeting & Swaps

```html
<button
  heimdall-content-click="Cart.Add"
  heimdall-content-target="#cart"
  heimdall-content-swap="inner">
</button>
```

Swap modes:

* `inner` (default)
* `outer`
* `beforeend`
* `afterbegin`
* `none`

---

## Request Synchronization

Heimdall can coordinate overlapping action requests without adding a client dependency.

```html
<input
  heimdall-content-input="Search.Query"
  heimdall-content-target="#results"
  heimdall-debounce="250"
  heimdall-sync="replace"
  heimdall-sync-group="search">
```

Supported strategies:

* `parallel` runs requests independently and is the default
* `replace` cancels the active request and runs the newest request
* `drop` ignores the new request while another request is active
* `queue-latest` keeps only the latest pending request

Without `heimdall-sync-group`, synchronization is scoped to the triggering element. Give multiple elements the same group when their requests must coordinate.

Programmatic invocations use the same coordinator:

```js
const controller = new AbortController();

const result = await Heimdall.invoke("Search.Query", { query: "heimdall" }, {
  target: "#results",
  sync: "replace",
  syncGroup: "search",
  signal: controller.signal
});
```

Expected cancellation resolves normally with `cancelled: true` and a `cancelReason`; it is not reported as a Heimdall error.

---

## Payload Resolution

### Static JSON

```html
heimdall-payload='{"id":1}'
```

### Closest form

```
heimdall-payload-from="closest-form"
```

### Self dataset

```
heimdall-payload-from="self"
```

### Global reference

```
heimdall-payload-ref="App.State.Filters"
```

### Closest state

```
data-heimdall-state='{}'
heimdall-payload-from="closest-state"
```

Keyed:

```
data-heimdall-state-filters='{}'
heimdall-payload-from="closest-state:filters"
```

---

## Out-of-Band Updates (Invocation)

Server responses may include:

```html
<invocation heimdall-content-target="#cart">
  <template>...</template>
</invocation>
```

Invocation blocks:

* Are processed separately
* Never rendered directly
* Can update any allowed target
* Support swap modes

Scripts are stripped for safety.

---

## JavaScript Void Invocation

Server responses may include JavaScript void invocation directives:

```html
<javascript
  function="window.App.toast.success"
  args='["Saved"]'
  timing="after">
</javascript>
```

Rules:

* `function` must be an explicit dotted path rooted at `window.`, `globalThis.`, or `document.`
* `args` must be a JSON array
* `timing` is `after` by default, or `before` to run before response swaps
* Return values are ignored
* The directive element is stripped and never rendered
* `<redirect>` is a hard stop and prevents JavaScript invocation

Heimdall does not evaluate JavaScript source from responses.

---

## SSE (Bifrost)

Real-time HTML streaming via EventSource.

```html
<div
  heimdall-sse="orders"
  heimdall-sse-target="#orders"
  heimdall-sse-swap="beforeend">
</div>
```

Features:

* Auto reconnect
* Token-gated subscription
* Shared EventSource connections per topic
* Named SSE event dispatch with `heimdall-sse-event`
* OOB processing supported
* Works alongside normal actions

Multiple hosts on the same topic share one underlying EventSource. `heimdall-sse-event` filters which named server event a host handles inside that topic stream.

---

## Configuration

Global config:

```js
Heimdall.config.debug = true;
Heimdall.config.observeDom = true;
Heimdall.config.oobEnabled = true;
Heimdall.config.requestSync = "parallel";
Heimdall.config.requestTimeoutMs = 0;
```

Endpoint overrides:

```js
Heimdall.config.endpoints.contentActions = "/custom";
```

`parallel` and a timeout of `0` preserve the existing request behavior. Set a positive timeout globally or pass `timeoutMs` to an individual `Heimdall.invoke` call to opt in.

---

## Lifecycle Events

Heimdall exposes a dependency-free integration surface through DOM events:

* `heimdall:request-config` allows payload, headers, target, swap, and synchronization configuration
* `heimdall:request-before` fires before each fetch attempt and is cancellable
* `heimdall:request-after` fires after a received response is processed
* `heimdall:request-finally` always fires
* `heimdall:request-cancel` reports expected cancellation
* `heimdall:request-timeout` reports timeout cancellation
* `heimdall:swap-before` fires before action, invocation, and SSE swaps and is cancellable
* `heimdall:swap-after` fires after an applied swap

Events raised for declarative actions originate from the triggering element and bubble to `document`. Cancel a request or swap with `event.preventDefault()`:

```js
document.addEventListener("heimdall:request-config", event => {
  event.detail.headers["X-Correlation-ID"] = crypto.randomUUID();
});

document.addEventListener("heimdall:swap-before", event => {
  if (event.detail.kind === "main" && shouldKeepCurrentContent()) {
    event.preventDefault();
  }
});
```

The existing `heimdall:before`, `heimdall:after`, `heimdall:error`, `heimdall:abort`, and `heimdall:redirect` events remain supported. `heimdall:abort` continues to represent the server `<abort>` directive; client request cancellation uses `heimdall:request-cancel`.

---

## Server Requirements

Expected endpoints:

* `POST /__heimdall/v1/content/actions`
* `GET /__heimdall/v1/csrf`
* `GET /__heimdall/v1/bifrost`
* `GET /__heimdall/v1/bifrost/token`

Provided by **HeimdallFramework.Server**.

---

## Design Philosophy

Heimdall is built around:

* Hypermedia
* Server rendering
* Progressive enhancement
* Strong typing (server side)
* Minimal client runtime
* Real-time HTML

Conceptually similar to:

* HTMX
* Hotwire
* LiveView

But designed specifically for ASP.NET.

---

## Versioning

`HeimdallFramework.Web` `3.0.5` is the current release and targets .NET 10. The Web and Server packages use matching release versions.

The public runtime is intended for real application use, but the framework is still young. Upgrade deliberately between major versions.

The client runtime currently reports `apiVersion = 1`, and the default server endpoints remain under `/__heimdall/v1/...`. That version identifies the Heimdall browser/server wire protocol, not the NuGet package generation.

Changes may still occur across major package versions in:

* Attribute names
* Runtime behavior
* Endpoint contracts
* SSE details

---

## License

[MIT](https://github.com/bradleyables22/Heimdall/blob/master/LICENSE)
