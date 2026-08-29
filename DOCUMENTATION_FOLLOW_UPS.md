# Documentation follow-ups

## Cross-origin frontend and HTML backend

When the documentation site is updated, add a dedicated deployment section for frontends that invoke a Heimdall HTML backend on a different origin or domain.

Cover:

- The request headers Heimdall may send, including `X-Heimdall-Content-Action`, `RequestVerificationToken`, the opt-in `X-Heimdall-Client-Info`, application headers produced by `Heimdall.config.requestHeaders` such as `Authorization`, and JSON or multipart content types.
- The ASP.NET Core CORS policy needed to allow the frontend origins, methods, and Heimdall headers. Avoid recommending an unrestricted origin policy for authenticated applications.
- How browser preflight requests work and the latency developers should expect. Heimdall action requests already use non-safelisted headers, so cross-origin actions generally require preflight even when client information is disabled.
- The interaction between CORS, antiforgery, authentication, cookies, and `Access-Control-Allow-Credentials`.
- JWT examples using the asynchronous request-header provider, including its URL/kind filtering, fail-closed behavior, Bifrost token-minting coverage, and `heimdall:unauthorized` navigation hook.
- The runtime's current `credentials: "same-origin"` behavior. Verify and document the supported cross-origin authentication model before claiming that cookie-authenticated actions work across domains; consider whether a configurable credentials mode is needed.
- A complete ASP.NET Core configuration example and a troubleshooting checklist for rejected preflights, missing headers, missing cookies, and mismatched frontend/backend antiforgery settings.
