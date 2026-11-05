# Heimdall.E2E.TestPlugin

A standalone Razor Class Library used by `Heimdall.E2E` to test frontend plugins against a real ASP.NET
Core host. It is a fixture project, not a published package. It references the public server/rendering API;
its browser module uses the public plugin API and is delivered through ordinary RCL static web assets.
It does not import or copy Heimdall's private browser modules or bundle another runtime.

The consuming application's responsibilities are visible in `Heimdall.E2E/Program.cs` and
`Heimdall.E2E/wwwroot/js/e2e-test-plugin.js`:

```csharp
builder.Services.AddHeimdallE2ETestPlugin(options => options.Label = "Configured by E2E host");
builder.Services.AddHeimdall(/* host configuration */);
```

```javascript
import testPlugin from "/_content/Heimdall.E2E.TestPlugin/js/test-plugin.js";
Heimdall.use(testPlugin, { instancePrefix: "host", initializeDelayMs: 20 });
```

`AddHeimdallE2ETestPlugin` registers its invocation assembly additively, its own options, and scoped
services. The host calls `AddHeimdall` and owns page routing and runtime loading. The library supplies
`TestPluginWorkbench.Render()`, its asset paths, and both static and instance invocation methods.

Open `/e2e/plugins` in the fixture app for the minified runtime or `/e2e/plugins?runtime=debug` for the
readable runtime. The page contains controls and visible lifecycle/command/error logs for manual use.
Ctrl+Enter in the editor uses a custom trigger and independent payload reader. The other controls exercise
built-in clicks, subscriptions, server commands, async initialization, option changes, appended/replaced
elements, cleanup, abort, error isolation, and ordered Bifrost commands. Each page uses a fresh SSE topic.

From `Heimdall.E2E`, run:

```sh
npm run test:plugins
```

The hosted tests run each plugin scenario against both runtime assets, using real requests, payload
binding, antiforgery validation, library discovery, scoped DI, and SSE delivery. The full `npm test` suite
includes them. `HEIMDALL_E2E_SKIP_BUILD=1` reuses a previously built fixture app.

The plugin and host registration module are also checked by `Heimdall.Web`'s `npm run test:types`, using
the shipped `heimdall-plugins.d.ts` contract and a compile-time mapping for the RCL's public URL.
