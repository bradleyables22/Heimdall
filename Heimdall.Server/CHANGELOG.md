# HeimdallFramework.Server changelog

## [3.0.9] - 2026-08-30

### Added

- Added multipart content-action binding for form payloads and `IFormFile`, `IFormFileCollection`, array, and generic file collection parameters.
- Added typed FluentHtml helpers for multipart forms and native file-input attributes, with ASP.NET Core request-size and form-limit metadata support.
- Added `LocalizeTime` for absolute `DateTimeOffset` and UTC/local `DateTime` values with validated C#-style formats and server-rendered fallback text.
- Added ordered `<mutation>` response directives and fluent builders for attributes, classes, and Heimdall state across self, subtree, and selector scopes.
- Added push/replace history response helpers and fluent page-lifecycle trigger helpers for document-visible and online behavior.
- Added `Lang` and scoped transitions between FluentHtml element/fragment builders and Heimdall builders.
- Added global `EnableAntiforgery` configuration and native `[RequireAntiforgeryToken]` metadata at the action or declaring-type level. Global opt-out also covers Bifrost token minting and does not require antiforgery services or middleware.
- Added the bounded `HeimdallClientInfo` framework parameter alongside normal payload and `HttpContext` binding.
- Added public OpenTelemetry-compatible activity and metric names for content actions and Bifrost.
- Added `Bifrost.HasSubscribers(topic)` as an instantaneous local-instance optimization hint.

### Fixed

- Returned consistent `400 Bad Request` or `413 Payload Too Large` results for malformed multipart bodies, missing required files, and ASP.NET Core request/form-limit violations before invoking application actions.
- Resolved antiforgery services only when validation is enabled, allowing globally disabled action and Bifrost flows to run without `AddAntiforgery()` or `UseAntiforgery()`.
- Disposed Bifrost request-abort registrations and kept unsubscribe cleanup safe when connections close.

## [3.0.8] - 2026-08-01

### Fixed

- Made content-action identifiers case-insensitive throughout registration and resolution.

## [3.0.7] - 2026-07-19

### Added

- Request synchronization, cancellation, lifecycle events, swap hooks, native command helpers, and `IHtmlContent.ToHtmlString()`.
