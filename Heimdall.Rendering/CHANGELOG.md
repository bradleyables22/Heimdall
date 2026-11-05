# HeimdallFramework.Rendering changelog

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

