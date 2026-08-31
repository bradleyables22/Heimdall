# Changelog

Notable changes to the Heimdall solution are recorded here. Package-specific details live in each library project's changelog.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and released packages use semantic versioning.

## [3.0.9] - 2026-08-30

### Added

- Added multipart form and file-upload support with typed file-input helpers, `IFormFile` binding, native ASP.NET Core form/request limits, and queued file snapshots.
- Added browser-local time rendering with C#-style formats, language boundaries, server fallbacks, lifecycle hooks, and localization across initial, action, OOB, and SSE HTML.
- Added ordered mutation directives for attributes, classes, and Heimdall state without replacing existing DOM nodes.
- Added browser history push/replace directives with root-relative normalization, lifecycle events, and managed Back/Forward handling.
- Added document-visible and online action triggers plus the client-only `heimdall:offline` event.
- Added global, declaring-type, and per-action antiforgery policy controls, including a complete client opt-out for non-cookie security models.
- Added bounded `HeimdallClientInfo` action binding, asynchronous request-header providers, and cancellable unauthorized-response handling.
- Added content-action and Bifrost diagnostics through `ActivitySource` and `System.Diagnostics.Metrics`, plus the local advisory `Bifrost.HasSubscribers` check.
- Added the native `Lang` fluent helper and transitions between normal FluentHtml and Heimdall-specific builders.

### Fixed

- Prevented replaced or stale requests from applying responses after a newer request owns the synchronization group.
- Corrected queue-latest payload behavior so form fields and files keep their submission snapshot while closest-state bindings refresh safely when execution begins and remain stable across antiforgery retries.
- Re-resolved selector targets after queued or OOB DOM replacement, cancelled requests whose direct target or state source disappeared, and preserved lifecycle target overrides.
- Kept disabled/busy UI state active until the current replacement request actually finishes.

## Published package baseline

- `HeimdallFramework.Server` 3.0.9 — 2026-08-30
- `HeimdallFramework.Web` 3.0.9 — 2026-08-30
- `HeimdallFramework.Bootstrap` 5.0.1 — 2026-06-24
