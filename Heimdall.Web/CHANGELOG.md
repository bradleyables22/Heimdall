# HeimdallFramework.Web changelog

## [3.0.9] - 2026-08-30

### Added

- Added automatic multipart submission for forms containing file inputs and programmatic `FormData` payload support.
- Added browser-local time formatting for initial documents, action swaps, OOB invocations, observed DOM additions, and Bifrost SSE fragments, with language boundaries and lifecycle events.
- Added ordered mutation processing for attributes, classes, and state while preserving node identity, focus, listeners, and browser-owned input state.
- Added history push/replace processing, normalization of leading-slash and rootless URLs, lifecycle events, and managed Back/Forward reload behavior.
- Added document-visible and online action triggers plus the local-only `heimdall:offline` event.
- Added `Heimdall.config.antiforgery` so globally opted-out applications skip CSRF token requests, headers, and retries for actions and Bifrost token minting.
- Added opt-in browser capability snapshots through `Heimdall.config.clientInfo` and the mutable/cancellable `heimdall:client-info-before` event.
- Added synchronous or asynchronous `Heimdall.config.requestHeaders` resolution for content, CSRF, and Bifrost token requests, including cancellation and fail-closed behavior.
- Added the cancellable `heimdall:unauthorized` event for raw `401` responses.

### Fixed

- Prevented replaced and stale requests from applying late responses after a newer request owns the synchronization group.
- Kept only the latest queued request while preserving its own payload source, lifecycle changes, and synchronization scope.
- Snapshotted form fields and file objects at submission while refreshing closest-state and keyed-state bindings safely when queued execution begins.
- Preserved the refreshed payload across antiforgery retries instead of silently reading newer DOM state.
- Re-resolved selector targets after queued or OOB replacement, cancelled disconnected direct targets or removed state sources, and retained request-before target overrides.
- Kept disabled/busy state active across replacement handoff until the current request completes.

## [3.0.8] - 2026-08-01

### Fixed

- Matched case-insensitive content-action behavior in the browser runtime.

## [3.0.7] - 2026-07-19

### Added

- Parallel, replace, drop, and queue-latest request coordination.
- Request, cancellation, timeout, swap, and runtime lifecycle events.
