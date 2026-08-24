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

For framework usage and examples, see [heimdall-framework.org](https://heimdall-framework.org).
