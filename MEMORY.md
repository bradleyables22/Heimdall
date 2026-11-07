# Project memory

## Deferred Bootstrap work — 2026-11-07

The user asked to preserve the Bootstrap implementation review and revisit it later. Keep the Bootstrap
package unchanged during the 5.1.0 template adoption work. See
[Bootstrap modernization review](docs/plans/bootstrap-modernization.md) for findings, evidence, and the
recommended order of work.

## Rendering conventions accepted by the user

- Use the `Heimdall` fluent facade for Blazor `@attributes` and the shared `heimdall` Tag Helper in
  MVC/Razor Pages `.cshtml` where applicable. These emit canonical long-form runtime attributes.
- The optional `hd.shortcuts` JavaScript plugin supplies shorter hand-authored HTML attributes.
  Application and plugin aliases share the core registry; original identities remain the source of truth.
- Attribute aliases apply to normal element behavior and plugin triggers. Response directive names
  remain canonical.
- The user reported Rendering, Server, and Web 5.1.0 deployed on 2026-11-07 and requested template
  packages and their corresponding projects be updated to use the released APIs.
- Template release scope is the unified `HeimdallFramework.Templates` package and its five starters,
  including their authentication and SSG output variants. The user explicitly excluded the retired
  standalone MVC and SSG template packages from this update.

## Unified template adoption completed — 2026-11-07

- Updated `../Heimdall-Template-App` to `HeimdallFramework.Templates` 5.1.0. All 15 starter project
  definitions use Server/Web 5.1.0 where applicable; Bootstrap remains 5.0.1.
- Blazor uses `@attributes` and MVC/Razor Pages use the `heimdall` Tag Helper with the shared fluent
  API. SSG views include the rendering import and Tag Helper registration. Output stays canonical.
- Fixed the Razor Pages SSE invocation to accept its `HttpContext` parameter and marked MVC content
  invocations `[NonAction]`. Razor model references also support `Heimdall.*` project namespaces.
- Installed the packed templates into isolated hives. All ten authentication/output variants built
  without warnings. Browser checks passed for all four unauthenticated hosted starters, including
  state, forms, lazy loading, OOB and SSE; both SSG output modes generated the expected pages.
- The local package, checksum, changelog and validation manifest are in
  `artifacts/template-releases/5.1.0/`. The template package has not been pushed to NuGet.

## OIDC starter corrections completed — 2026-11-07

- The user explicitly rejected the runtime `Oidc:Enabled` switch. Authentication is selected only
  during template generation. OIDC output always registers cookies, OIDC and the fallback policy;
  no-auth output omits the entire scaffold. Invalid/missing provider settings fail startup.
- All hosted logout forms now supply antiforgery tokens, and POST sign-out validates them before
  clearing the session. Authentication/authorization precede antiforgery middleware.
- Added public signed-out, authentication-error and access-denied pages. Provider logout retains
  an ID-token hint; providers without a discovered logout endpoint use local logout. Result URLs
  honor the application's path base. Provider errors are logged without exposing them in the page.
- Added configurable scopes and name/role claim mapping, secure-secret instructions and a generated
  `Authentication/README.md` with provider-specific setup guidance.
- Added `Heimdall.Template.Tests` to the unified template repository: 21 integration cases pass.
  Browser login/code exchange/PKCE, authenticated counter invocation, logout token rejection and
  provider logout passed for all four OIDC starters against a local test provider. All ten generated
  variants build without warnings; no-auth omission is verified. Real provider round trips remain
  provider-specific integration work. The 5.1.0 template package and its verification manifest were
  refreshed locally and have not been published.
