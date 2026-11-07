# HeimdallFramework.Rendering changelog

## [5.1.0] - 2026-11-07

### Added

- Added the `Heimdall` fluent entry point with the complete element behavior API and immutable standalone configurations usable directly with Blazor `@attributes`.
- Added a shared `heimdall` Razor Tag Helper for MVC and Razor Pages, preserving explicit attributes, normal content, and other Tag Helpers.
- Added local-time attribute configurations with separate fallback text, automatic Razor fallback rendering, full fragment/directive entry points, asynchronous fragment preparation, and typed Blazor content adapters.
- Added fluent `SyncGroup` and `LocalizeTime` methods, public read-only attribute enumeration, and API parity checks with compiled Blazor and MVC integration fixtures.

### Fixed

- Preserve explicit `false` for Heimdall `PreventDefault`, `VisibleOnce`, and `SseDisable` options so the runtime can override defaults and repeated fluent calls can switch earlier values off.

## [4.0.1] - 2026-11-05

### Added

- Added `OnPluginTrigger`, `PayloadFromPlugin`, and encoded `PluginCommand` helpers.
- Added `InvocationId`, retaining `ActionId` compatibility.

## [4.0.0]

### Added

- Extracted the `Html` core and `FluentHtml` server-side rendering APIs from `HeimdallFramework.Server`.
- Added the typed `HeimdallHtml` and `FluentHeimdall` markup-authoring APIs, including triggers, payloads, state, swaps, mutations, storage, SSE markup, and response directives.
- Added synchronous and asynchronous FluentHtml element, fragment, and nested builder composition.
- Added `ToHtmlString()` and `ToHtmlStringAsync()` materialization helpers.

