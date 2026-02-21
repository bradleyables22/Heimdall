# HeimdallFramework.Web

> **Status: ALPHA — APIs and behavior may change**
>
> **HeimdallFramework.Web** distributes the **Heimdall JavaScript runtime (`heimdall.js`)** as a Razor Class Library (RCL) static web asset.
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

* `heimdall.js` runtime
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

Reference:

```html
<script src="/_content/HeimdallFramework.Web/heimdall.js"></script>
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
* OOB processing supported
* Works alongside normal actions

---

## Configuration

Global config:

```js
Heimdall.config.debug = true;
Heimdall.config.observeDom = true;
Heimdall.config.oobEnabled = true;
```

Endpoint overrides:

```js
Heimdall.config.endpoints.contentActions = "/custom";
```

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

Alpha releases may change:

* Attribute names
* Runtime behavior
* Endpoint contracts
* SSE details

Avoid long-term API assumptions until v1.

---

## License

MIT
