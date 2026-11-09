# Functions E2E host

Runs Heimdall through the actual Azure Functions isolated worker and ASP.NET Core HTTP bridge. This is a test fixture, not a production starter: `X-E2E-User` deliberately supplies a test identity. Do not deploy that authentication handler.

From this directory, with .NET 10 and Node installed:

```powershell
npm ci
Copy-Item local.settings.example.json local.settings.json
dotnet build
npx playwright install chromium
npm test
```

Core Tools is installed locally by `npm ci`. The runner starts the built host on port 7079, runs Chromium checks, and stops the process tree in `finally`. HTTP-only triggers do not require a storage connection for these checks; for additional bindings configure Azurite or an appropriate development storage account in the ignored `local.settings.json`.

Checks cover JSON form content invocations, automatic antiforgery, multipart uploads, main and out-of-band swaps, live SSE, token-refresh reconnect, rejection of unprotected requests, and incremental initial-event/idle-heartbeat/event delivery while the function is still executing.

The fixture also exercises the application asset handler, the Heimdall.Web runtime RCL and a separate test RCL through SDK static asset endpoints, compiled MVC/Razor partials, and static Blazor rendering. Set `HEIMDALL_FUNCTIONS_E2E_SCRIPT_ROOT` to a published output directory to run the same HTTP checks against publish layout.

```powershell
npm run test:keys
```

Key mode starts Core Tools with `--enableAuth`, generates a random function-scoped key in a private temporary directory, and checks missing/wrong/header/query keys plus browser and SSE behavior. The process tree and key directory are cleaned up afterward. For an external keyed test deployment, supply its test key through `HEIMDALL_E2E_FUNCTION_KEY`.

To validate the unified template package and generated applications (requires the sibling `Heimdall-Template-App` repository):

```powershell
npm run prepare:starters
npm run test:starter
npm run test:starter:oidc
```

Preparation packs locally, installs into a fresh isolated template hive, generates both variants, restores into a fresh package directory, builds with warnings treated as errors, and checks published assets/worker metadata. It writes `artifacts/functions/starters.json`, which the browser runner reads. Set `HEIMDALL_FUNCTIONS_STARTER_PROJECT` to test another generated project.

Starter tests use local HTTPS and a short-lived certificate file without installing/trusting it. The OIDC test starts a loopback provider, validates code exchange and PKCE, checks protected UI/protocol behavior, exercises counter/SSE behavior, rejects unprotected logout, and completes provider logout with an ID-token hint. PowerShell 7 is required for certificate creation. No cloud resources are created; real-provider-specific configuration remains separate.

```powershell
npm run test:soak
```

Soak mode holds the stream for at least 250 seconds. Set `HEIMDALL_FUNCTIONS_SOAK_MS` to change the duration (keep below the host's configured 10-minute execution timeout).

To run against an already running, **isolated test deployment** of this same fixture, set `HEIMDALL_FUNCTIONS_E2E_URL` to its origin before running the tests. The runner will not start or stop an external host. These tests run test content invocations and publish events. They are not a read-only probe of production.

Local soak success does not establish Azure load-balancer or gateway behavior. Run through the intended ingress to establish hosting support. No test command deploys or publishes anything.

For manual inspection, `npm start` builds/starts the worker; open `http://localhost:7079/api/demo`.
