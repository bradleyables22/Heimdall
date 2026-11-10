# Page backends and CSRF

Requires Server/Web 5.1.2, Functions 1.0.3-preview, and Templates 5.1.5 for the corresponding features.

Configure a single trusted backend per HTML document, after the runtime script loads and before DOMContentLoaded:

```javascript
const root = "https://orders.example.com/__heimdall/v1";
Object.assign(Heimdall.config.endpoints, {
    contentInvocations: `${root}/content/invocations`,
    csrf: `${root}/csrf`,
    bifrostToken: `${root}/bifrost/token`,
    bifrost: `${root}/bifrost`
});
Heimdall.config.credentials = "include";
```

The default is `same-origin`. `include` opts into cookies for invocations, both token endpoints, and EventSource. These are the only supported modes: native EventSource cannot implement `omit` for same-origin cookies. Full MPA navigation resets tokens and connections; fragment swaps preserve the page's backend. Configure once per document, not during active requests. `basePath` does not recalculate endpoint URLs. Script source origin does not select the backend.

Filter `requestHeaders` by the trusted backend origin before attaching bearer tokens or function keys. Native EventSource does not use custom headers. Keep all four endpoints on the intended trusted backend. Remote HTML and response directives join the shell's trust boundary; this is not isolation for untrusted services.

## Cross-origin hosting

Allow only the shell's exact origin and the required methods/headers. Credentialed CORS requires `Access-Control-Allow-Credentials: true`, never a wildcard origin. Allow `Content-Type`, `X-Requested-With`, `X-Heimdall-Content-Invocation`, `RequestVerificationToken`, and optional headers such as `Authorization` or `X-Heimdall-Client-Info` when used. Expose `X-Heimdall-Antiforgery` through `Access-Control-Expose-Headers`, including on error responses.

ASP.NET Core hosts can configure `WithOrigins`, `WithMethods`, `WithHeaders`, `WithExposedHeaders("X-Heimdall-Antiforgery")`, and `AllowCredentials` in a CORS policy, then run CORS before authentication/authorization. Functions apps configure equivalent behavior at their host or gateway; they do not run the ASP.NET Core middleware pipeline.

Truly cross-site cookies additionally require appropriate `SameSite=None` and `Secure` policies for both antiforgery and authentication cookies, and HTTPS. Third-party-cookie blocking can still prevent access. Prefer same-site hosting or a same-origin gateway if cookies cannot be used reliably. Cross-origin configuration does not disable CSRF. Bearer-only apps with no ambient authentication can deliberately choose a different model and disable antiforgery on both server and browser.

## Safe recovery

Only HTTP 400 with `X-Heimdall-Antiforgery: invalid` triggers a token refresh and one retry. The server sets this marker when validation rejects a request before application execution. Ordinary HTTP 400 bodies mentioning CSRF are never retried. Concurrent failures of an old token reuse an already refreshed token. Missing CORS exposure or an older server without the marker causes the failure to be returned without guessing. Deploy updated Server/Functions before Web; old clients still understand the retained error bodies.

## Functions scale-out

All instances of one logical backend need the same persistent key repository and application name. Different backends/environments should remain separate. The browser stores a CSRF request token; it does not store the key ring or select a Function instance.

The starter's `Hosting/DataProtectionSetup.cs` supports these Function App settings:

```text
DataProtection__ApplicationName=OrdersProduction
DataProtection__BlobUri=https://ACCOUNT.blob.core.windows.net/keys/orders.xml
DataProtection__ManagedIdentityClientId=OPTIONAL_USER_ASSIGNED_CLIENT_ID
```

Create the container and grant the Function App identity Storage Blob Data Contributor scoped to the repository. The provider creates the key blob. Use a URL without SAS, query, or fragment. Production uses managed identity (system-assigned by default); Development with an explicit BlobUri uses DefaultAzureCredential. Azure.Core 1.55.0 supplies the Azure.Identity credential types; a separate older Azure.Identity package conflicts with them.

Alternatively, `DataProtection__KeyDirectory` can point to a persistent shared mount. It is mutually exclusive with BlobUri. Equal instance-local paths do not share storage. With neither configured, ASP.NET Core host-specific defaults apply and local development needs no Azure account. Verify persistence and sharing on the deployed plan/slot; `AzureWebJobsStorage` is not automatically a Data Protection repository.

Retain old keys across rotation/restart. Restrict repository access. Storage encryption at rest is distinct from application-level wrapping; add `ProtectKeysWithAzureKeyVault` if the deployment requires wrapping, retaining the necessary key versions. Explicit filesystem storage does not automatically encrypt key XML. Shared keys do not distribute Bifrost messages; backplanes remain application-owned.

## Verification

- `dotnet test Heimdall.Functions.Tests`: isolated/shared keys, identity/application-name boundaries, retained keys through rotation/restart, and explicit rejection markers.
- `npm test` in Heimdall.Web: both browser bundles, bounded retries, concurrent refresh, and credential configuration.
- `npm run test:cross-origin`: two actual local HTTP origins, browser CORS, cookies, exposed rejection headers, SSE, full-page reset, and CORS denial. Tests same-site cross-origin transport, not third-party cookie support.
- Template preparation and starter browser tests verify generated project startup, assets, invocations, and SSE. Live managed identity and Blob permissions must be verified against deployment resources.
