# Heimdall browser runtime source

This directory contains the maintainable source for the Heimdall browser runtime. Product usage belongs in the [Heimdall documentation](https://heimdall-framework.org); this README covers only the local contributor workflow.

The build uses `heimdall.entry.js` as its entry point and writes the readable and minified browser bundles under `Heimdall.Web/wwwroot`. Files in `wwwroot` are generated output and should not be edited directly.

## Build

From `Heimdall.Web`:

```bash
npm ci
npm run build
```

The .NET build and packaging workflows also invoke the browser build when needed.

## Verify

```bash
npm run verify
```

This checks that generated bundles are current and runs the browser-runtime tests. To include package-shape verification, run:

```bash
npm run verify:all
```

If Playwright's Chromium binary is not installed locally:

```bash
npm run install:browsers
```

## Contributor notes

- Preserve observable behavior unless the change is intentional and covered by tests.
- Keep `heimdall.entry.js` as the runtime entry point.
- Prefer focused modules under `core/`.
- Regenerate bundles through the build rather than editing generated files.
