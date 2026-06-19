# Heimdall.E2E

Browser-level fixture app for validating Heimdall against a real ASP.NET Core host.

Run from this directory:

```bash
npm run test
```

The runner builds `Heimdall.E2E.csproj`, starts the app on a random local HTTP port, launches Chromium with Playwright, and drives real page interactions.

The app maps both `/` and `/e2e` to a dedicated harness page with stable element IDs for load triggers, dynamic booting after swaps, swap modes, out-of-band invocation, abort, redirect, closest-state payloads, form payloads, payload refs, self payloads, delegated events, event modifiers, programmatic invokes, JavaScript invocation, detailed action errors, and Bifrost SSE scenarios including OOB and JavaScript directives.

The same host also registers static generation fixtures. The E2E runner invokes `dotnet run -- --heimdall-generate-static` against a temporary `HEIMDALL_E2E_STATIC_OUTPUT` directory, verifies generated pages, assets, manifest, sitemap, robots.txt, clean-output behavior, and serves the result from `/e2e-static` for browser assertions.

If this project has not installed its own Node dependencies, the runner falls back to the Playwright dependency under `../Heimdall.Web/node_modules`.
