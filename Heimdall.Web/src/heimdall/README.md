# Heimdall Runtime Source

This folder contains the maintainable source sections for the browser runtime.

`wwwroot/heimdall.js` remains the source-of-truth reference file for now. The build pipeline reads these sections, passes the assembled runtime through esbuild, and writes `wwwroot/heimdall-bundle.js`.

Consumers should still reference only the public RCL asset:

```html
<script src="/_content/HeimdallFramework.Web/heimdall.js"></script>
```

## Rules

- Preserve behavior exactly unless a change is intentional.
- Preserve existing quirks while refactoring.
- Keep the section order in `../../build.mjs`.
- Do not write generated output over `wwwroot/heimdall.js` while it is the reference file.
- Move toward true `import` / `export` modules once the bundle is trusted and covered by tests.

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

Verification checks that the source sections still assemble to `wwwroot/heimdall.js` byte-for-byte, that `wwwroot/heimdall-bundle.js` is up to date, and that the browser parity tests pass against both runtime files.

The first local test run may need the Playwright browser binary:

```powershell
npm run install:browsers
```

The parity tests live in `tests/heimdall-runtime.spec.mjs`.
