# Heimdall.E2E

Browser-level tests for Heimdall running against a real ASP.NET Core host.

The fixture application exercises representative server, browser-runtime, real-time, and static-generation behavior. The test suite is the authoritative inventory of covered scenarios; this README intentionally does not duplicate that list.

## Run

From this directory:

```bash
npm ci
npm test
```

The runner builds the fixture application, starts it on a temporary local port, and drives it with Playwright. It also uses temporary output locations for static-generation checks.

## Standalone test plugin

[`Heimdall.E2E.TestPlugin`](../Heimdall.E2E.TestPlugin/README.md) is a separate Razor Class Library consumed
through its registration extension, rendering API, and `_content` JavaScript/CSS assets. Open `/e2e/plugins`
for the interactive workbench; use `?runtime=debug` to select the readable browser bundle.

```bash
npm run test:plugins
```

The plugin tests cover real invocation discovery, scoped services and host options, custom triggers and
payload readers, behavior initialization and cleanup, commands, antiforgery, request cancellation, and
Bifrost ordering. Every scenario runs against both readable and minified runtime assets. They are also
included in the full suite. `--filter=...` or `HEIMDALL_E2E_FILTER` selects scenarios by name, and
`HEIMDALL_E2E_SKIP_BUILD=1` reuses a previously built app.

For framework usage and examples, see [heimdall-framework.org](https://heimdall-framework.org).
