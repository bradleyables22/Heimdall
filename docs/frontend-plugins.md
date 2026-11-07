# Frontend plugins

Attribute naming is shared with core through the [attribute registry and optional shortcuts plugin](attribute-shortcuts.md).

Heimdall's plugin API extends browser interaction through managed element behaviors, custom triggers,
payload sources, and registered response commands. Install plugins explicitly:

```javascript
import editorPlugin from "/js/editor-plugin.js";

Heimdall.use(editorPlugin, { commitKey: "Enter" });
```

The complete working module is [editor-plugin.js](../Heimdall.Web/examples/editor-plugin.js).
It adds Ctrl+Enter submission to a server-rendered textarea, provides its payload reader, and handles a
server command to set its value. It has no runtime dependencies beyond Heimdall and browser DOM APIs.

For a complete library/host integration, see [Heimdall.E2E.TestPlugin](../Heimdall.E2E.TestPlugin/README.md).
That standalone RCL supplies assets, rendering, and invocations to the E2E workbench at `/e2e/plugins`,
where the same plugin contracts are exercised over real HTTP and Bifrost connections.

## Loading and distribution

Load the core runtime in the document head before application modules, so registration happens before
Heimdall's DOMContentLoaded boot. Keep the runtime script synchronous in this setup:

```html
<script src="/_content/HeimdallFramework.Web/heimdall-bundle.min.js"></script>
<script type="module" src="/js/install-plugins.js"></script>
```

An RCL can distribute a plugin's JavaScript and CSS under its `_content/{PackageId}` path. Applications
still load those assets and call `Heimdall.use`. Bundled applications can import the same plugin module;
the plugin package must use the application's runtime rather than bundle another Heimdall instance.
Classic browser bundles can export the plugin object on their own global and register it explicitly.
Heimdall does not download scripts, styles, or dependencies during registration.

Late installation is supported: matching elements already in the document are initialized immediately.
Commands received before their handler is installed are reported and skipped, without later replay.
Install required plugins before any initial `load` invocations that consume their payloads or commands.

## Identity, compatibility, options, and ownership

```javascript
export default {
    id: "company.editor",
    version: "1.0.0",
    pluginApiVersion: 1,
    requires: [],
    install(api, options) {
        // Register extensions synchronously. Return optional plugin-wide cleanup.
    }
};
```

`id` is required, lowercase, and composed of dotted or hyphenated alphanumeric segments. The `heimdall`
namespace is reserved. `pluginApiVersion: 1` specifies the frontend SDK contract, independently of the
HTTP protocol's `Heimdall.apiVersion`. A package `version` is optional diagnostic metadata, not a
compatibility check. `requires` lists installed plugin ids; the application controls dependency order
and package versions. Missing dependencies and incompatible plugin API versions throw during installation.

`Heimdall.use(plugin, options)` returns Heimdall for chaining. Repeating the exact plugin object and options
reference is a no-op. A different object or options under the same id throws. Plugins validate and apply
defaults to their own options; options are not implicitly merged into global configuration.

Every extension's local `name` is qualified with the plugin id. Registering `set-text` inside `company.editor`
creates `company.editor.set-text`. Duplicate extension names within a capability and conflicting trigger
attributes throw. Installation stages registrations and commits them only when `install` succeeds.
Failures abort `api.signal`, invoke registered cleanup, and discard the staged registrations.
`install` is synchronous; asynchronous initialization belongs in element behaviors. Registries close
after `install` returns. Use `api.onDispose(callback)` or the returned cleanup function for plugin-wide
listeners, timers, subscriptions, and other resources. Event listeners can use `{ signal: api.signal }`.

`Heimdall.plugins.list()` provides metadata snapshots. `Heimdall.plugins.remove(id)` aborts the plugin
scope, disposes its element instances and subscriptions, and removes its registrations. It returns false
for an unknown id and throws when an installed plugin still depends on that id. Cleanup runs once per
active mount or subscription. Arbitrary side effects outside the managed API remain the plugin's responsibility.

The public [heimdall-plugins.d.ts](../Heimdall.Web/wwwroot/heimdall-plugins.d.ts) contains the authoring contract
and browser-global declarations. Reference it in a TypeScript project or through a JavaScript JSDoc import.
Minor additions to plugin API v1 are additive; incompatible signature or lifecycle changes require a new
plugin API major and an explicit migration. Private `_build` modules are not supported import paths.

## Element behaviors

```javascript
api.behaviors.register({
    name: "widget",
    selector: "[data-company-widget]",
    observedAttributes: ["data-widget-options"],
    mount(element, { signal }) {
        const widget = initializeWidget(element);
        return {
            update() { widget.configure(element.getAttribute("data-widget-options")); },
            dispose() { widget.destroy(); }
        };
    }
});
```

Heimdall initializes the matching root and all matching descendants, once per active match. Repeated
`boot` calls and DOM moves within the observed document preserve instances. Changed `observedAttributes`
call a synchronous instance `update`; an instance without `update` is disposed and remounted. Removing
the marker or changing selector membership disposes the instance. Avoid selectors whose membership
depends on unrelated UI state; explicit marker attributes make ownership predictable.

Mount may return a cleanup function, an object with optional `dispose`/`update`, nothing, or a promise of
those results. Each mount receives its own lifetime `signal`. Async readiness has a configurable bound;
its signal aborts on failure, timeout, removal, or plugin removal. Cleanup returned after removal still runs.
Failed mounts are reported and are not retried continuously on unrelated DOM changes. Explicitly remove
and restore the matching marker or change observed options to request a new mount.

For Heimdall swaps, cleanup happens after cancellable swap decisions and before replacing the affected
nodes. Appended content preserves existing instances. Every inserted root is initialized even with
`observeDom: false`. DOM changes outside Heimdall are reconciled by its observer; final removal is detected
asynchronously. When DOM observation is disabled, call `Heimdall.boot(root)` after external insertion and
`Heimdall.dispose(root)` before external removal. Shadow roots and other documents require their own integration;
the v1 automatic observer covers the main document.

Behaviors mount before initial load invocations read their plugin payloads. After-swap commands wait for
their target and related behavior readiness. Mount code must honor its signal to stop its own work;
cancellation cannot undo side effects already performed by external JavaScript.

## Custom triggers and payload sources

```javascript
api.triggers.register({ name: "commit", events: ["company-editor-commit"] });
api.payloads.register({
    name: "document",
    timing: "trigger",
    read: ({ element }) => ({ text: element.value })
});
```

Default trigger attributes replace dots in the qualified name with hyphens. A plugin can provide an
explicit `attribute` and an `aliases` array; their names are registered in the shared core attribute
registry. Plugins cannot claim original built-in names or conflicting aliases. Plugin-scoped
`api.attributes.alias` can also provide shortcuts for existing attributes. Native `events` are individual DOM
event names; `event` is shorthand for one name. Use `subscribe({ element, emit, signal })` instead for an SDK,
legacy library, timer, or other event source. It can return a synchronous cleanup function. Call
`emit(event)` to use markup payloads or `emit(null, { payload })` to submit an explicit snapshot.
`emit` resolves when the invocation finishes; a debounced emission superseded or disposed before it starts
resolves with `null`.

```html
<textarea id="editor" data-company-editor
    heimdall-content-company-editor-commit="documents.save"
    heimdall-payload-from="plugin:company.editor.document"
    heimdall-content-target="#save-status"
    heimdall-sync="replace"></textarea>
<div id="save-status"></div>
```

Custom triggers use the shared scope and ignore-boundary resolver, `heimdall-prevent-default`,
`heimdall-debounce`, disabled-element checks, synchronization, authentication headers, antiforgery,
targets, swaps, and request lifecycle. Ignore lists use the qualified trigger name or `*`. Optional
descriptor `filter`, `preventDefault`, and `debounceMs` provide plugin defaults. Trigger callbacks do not
send their own fetch requests. A removed subscription cannot emit new requests.

Payload readers are independent of triggers and work with built-in click, submit, and load triggers too.
`read` is synchronous and returns JSON-serializable data or FormData. Async preparation belongs in a
behavior. `trigger` (default) snapshots after debounce; `execution` re-reads when queued execution begins.
An explicit request-config payload override wins. Antiforgery retries reuse the submitted snapshot.
Missing or failed readers do not send an empty replacement payload. Queued execution bindings whose
element or registration disappears cancel as `payload-source-unavailable`.

The C# equivalents are `.OnPluginTrigger("company.editor.commit", "documents.save")` and
`.PayloadFromPlugin("company.editor.document")`, available through `.Heimdall()` and static helpers.
An explicit custom trigger attribute can be emitted with ordinary HTML attribute helpers.

## Response commands

```javascript
api.commands.register({
    name: "set-text",
    validate: args => args && typeof args.text === "string",
    handle({ element, args, signal }) { element.value = args.text; }
});
```

Emit commands using the encoded C# helper, which uses web JSON property naming:

```csharp
return FluentHtml.Fragment(fragment =>
{
    fragment.Add(SaveStatus.Render());
    fragment.Heimdall().PluginCommand(
        "company.editor.set-text",
        new { Text = "Saved content" },
        targetSelector: "#editor");
});
```

The wire directive is `<heimdall-command name="company.editor.set-text" args='{"text":"Saved content"}'
target="#editor" timing="after"></heimdall-command>`. Omitting `target` invokes once with `element: null`;
otherwise the first selector match is used. `all="true"` / `allTargets: true` applies to every matching element.
Targets are resolved at execution time; missing, invalid, or disconnected targets are reported and skipped.

Execution order is: redirect preflight; plugin `before` commands; existing before-JavaScript directives;
ordered OOB invocations and mutations; main swap; plugin `after` commands; existing after-JavaScript
directives. Commands preserve response order within their phase and await each handler's completion.
Existing JavaScript void directives retain their existing completion semantics. Commands inside HTML
templates are stripped without execution. Ordinary document boot never executes command markup.

A redirect suppresses all plugin commands. Failed HTTP responses strip command directives without
execution. `<abort>` suppresses the main swap while allowing commands. Cancellation or replacement
suppresses remaining response effects; an already performed effect cannot be rolled back. Async
handlers receive a signal and must check it before effects after their own awaits. Bifrost messages
with commands are serialized per subscription, including later messages while a command is pending;
pause or disconnect cancels pending command processing. Delivery is not an exactly-once guarantee,
and Heimdall never automatically retries a command handler.

## Failures and limits

Installation errors throw. Mount, payload, cleanup, subscription, and command errors emit
`heimdall:plugin-error` with plugin id, extension, phase, code, error, and available request/element context.
Command errors skip that command and allow subsequent commands to continue; they do not rewrite the HTTP
status or roll back prior DOM effects. Subscribe to diagnostics in application monitoring:

```javascript
document.addEventListener("heimdall:plugin-error", event => {
    reportPluginFailure(event.detail);
});
```

Default bounds are `pluginMountTimeoutMs: 10000`, `pluginCommandTimeoutMs: 10000`,
`pluginCommandMaxArgsChars: 65536`, and `pluginCommandMaxCount: 256` on `Heimdall.config`.
`pluginSseQueueLimit: 128` bounds pending messages per subscription; exceeding it reports
`sse-command-queue-full` and closes that subscription with `plugin-command-queue-full`.
Zero disables a timeout. Unknown commands, invalid JSON, rejected validation, missing targets, and timeouts
are diagnosed. Directives are removed before normal HTML insertion. Registered commands receive data;
Heimdall never evaluates server-supplied JavaScript source. Plugin scripts execute with normal page authority.

## Verification

The runtime conformance suite runs against readable and minified bundles. It exercises installation
rollback and conflicts, element moves/removal/updates, custom event and subscription triggers, built-in
triggers with plugin payloads, queued payload timing, async initialization, command ordering, failures,
redirects, abort, replacement, timeouts, and Bifrost ordering. Run `npm test` from `Heimdall.Web`; set
`HEIMDALL_TEST_FILTER=plugin` to focus on plugin contracts.
