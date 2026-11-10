# HeimdallFramework.Functions changelog

## [1.0.3-preview] - Unreleased

- Added explicit antiforgery rejection signals and bounded browser recovery without replaying business-validation failures.
- Added page-level cross-origin credentials for invocations, token requests, and SSE, retaining same-origin defaults.
- Documented shared Functions keys and per-page endpoint configuration with cross-origin regression coverage.

## [1.0.2-preview] - Unreleased

- Added opt-in package-supplied HTTP trigger plumbing so single-app templates expose only page registration.
- Corrected starter regression coverage to verify settings-driven startup, native pages, CSS/JS/fonts, and published assets without copying host configuration by hand.

## [1.0.1-preview] - Unreleased

- Targets Heimdall Server and Web 5.1.1 so the Functions package can be restored from the same published package set.

## [1.0.0-preview]

- Added the isolated-worker Azure Functions adapter for the Heimdall HTTP pipeline.
- Added page routing, static web asset dispatch, application asset serving, and MVC/Blazor rendering helpers.
- Added authentication, authorization, antiforgery, function-key, and Bifrost SSE integration helpers.
- Added preview support for the unified Functions starter and single catch-all HTTP trigger pattern.
