# HeimdallFramework.Web

**ALPHA RELEASE**

HeimdallFramework.Web is the distribution package for the **Heimdall JavaScript runtime** (`heimdall.js`).

This package contains **only** the client-side library (served as a Razor Class Library static web asset). It does not include Razor components, MVC helpers, or server middleware.

If you want server-side endpoints and the action pipeline, use **Heimdall.Server**.

---

## Use

Reference the script from the RCL static web assets path:

```html
<script src="/_content/Heimdall.Web/heimdall.js"></script>
```

That’s it. Heimdall will auto-boot on DOM ready.

---

## What it does

`heimdall.js` enables HTML-first server actions by:

- Listening for declarative attributes like:
  - `heimdall-content-click="Action.Id"`
  - `heimdall-content-submit="Action.Id"`
  - `heimdall-content-load="Action.Id"`
  - `heimdall-content-visible="Action.Id"`
  - `heimdall-content-scroll="Action.Id"`
- Posting JSON payloads to the Heimdall server endpoint
- Applying swaps (`inner`, `outer`, `beforeend`, `afterbegin`, `none`)
- Processing out-of-band updates via `<invocation>` directives
- Optional SSE support (“Bifrost”) using `EventSource`

---

## Requirements / Assumptions

Heimdall.js assumes a same-origin Heimdall server implementation that provides:

- **Content actions:** `POST /__heimdall/v1/content/actions`
- **CSRF token:** `GET /__heimdall/v1/csrf`
- **Bifrost (SSE):** `GET /__heimdall/v1/bifrost?topic=...`
- **Bifrost token:** `GET /__heimdall/v1/bifrost/token?topic=...`

These endpoints are provided by **Heimdall.Server** (recommended).

---

## Status

This package is currently **alpha**. APIs, attribute names, and behavior may change.

---

## License

MIT
