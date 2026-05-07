# Heimdall Runtime Source

This folder contains the maintainable source for the browser runtime.

The build pipeline reads `heimdall.entry.js`, bundles its ES module imports with esbuild, and writes `wwwroot/heimdall-bundle.js` plus `wwwroot/heimdall-bundle.min.js`.

Consumers should still reference only one public RCL asset. Use the readable generated runtime while debugging:

```html
<script src="/_content/HeimdallFramework.Web/heimdall-bundle.js"></script>
```

Use the minified generated runtime in production:

```html
<script src="/_content/HeimdallFramework.Web/heimdall-bundle.min.js"></script>
```

## Rules

- Preserve behavior exactly unless a change is intentional.
- Preserve existing quirks while refactoring.
- Keep `heimdall.entry.js` as the single runtime entrypoint.
- Treat `wwwroot/heimdall-bundle.js` and `wwwroot/heimdall-bundle.min.js` as generated output.
- Prefer small `import` / `export` modules for runtime code. Current modules live under `core/`.

## Build

From `Heimdall.Web`:

```powershell
npm run build
```

or:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-heimdall.ps1
```

`dotnet build`, `dotnet publish`, and `dotnet pack` also run the JavaScript build through `Heimdall.Web.csproj`.

## Verify

```powershell
npm run verify
```

Verification checks that generated bundles are up to date and that the browser tests pass against `wwwroot/heimdall-bundle.js` and `wwwroot/heimdall-bundle.min.js`.

The first local test run may need the Playwright browser binary:

```powershell
npm run install:browsers
```

The browser tests live in `tests/heimdall-runtime.spec.mjs`.
