# Attribute shortcuts

For typed C# authoring, see [Fluent attributes in Blazor and Razor](razor-blazor-authoring.md).

Load the optional shortcuts plugin after the core runtime to enable short attribute names:

```html
<script src="/_content/HeimdallFramework.Web/heimdall-bundle.min.js"></script>
<script src="/_content/HeimdallFramework.Web/heimdall-shortcuts.min.js"></script>
```

Both scripts can be loaded synchronously in the document head. The second script automatically installs
the `hd.shortcuts` plugin; no separate `Heimdall.use` call or attribute configuration is required.
Readable `heimdall-shortcuts.js` is also included. Loading it after startup reconciles existing markup.

```html
<form hd-submit="orders.save" hd-target="#save-status">
    <input name="Name" />
    <button type="submit">Save</button>
</form>
<div id="save-status"></div>

<form>
    <input type="search" name="Search"
           hd-input="orders.search" hd-from="closest-form"
           hd-target="#orders" hd-debounce="300" hd-sync="replace" />
</form>
<div id="orders"></div>
```

The registry reads aliases directly without copying or rewriting DOM attributes. Original names remain
accepted. If an element has both an original and an alias, the original wins, including empty values.
If several aliases are present, their registration order determines precedence. A trigger with several
recognized spellings is still resolved and mounted once per element.

## Included shortcuts

The preset covers all 38 built-in element attribute names and the named-state prefix:

| Original | Shortcut |
| --- | --- |
| `heimdall-content-click` | `hd-click` |
| `heimdall-content-submit` | `hd-submit` |
| `heimdall-content-input` | `hd-input` |
| `heimdall-content-change` | `hd-change` |
| `heimdall-content-keydown` | `hd-keydown` |
| `heimdall-content-blur` | `hd-blur` |
| `heimdall-content-hover` | `hd-hover` |
| `heimdall-content-load` | `hd-load` |
| `heimdall-content-visible` | `hd-visible` |
| `heimdall-content-scroll` | `hd-scroll` |
| `heimdall-content-document-visible` | `hd-document-visible` |
| `heimdall-content-online` | `hd-online` |
| `heimdall-content-target` | `hd-target` |
| `heimdall-content-swap` | `hd-swap` |
| `heimdall-content-disable` | `hd-disable` |
| `heimdall-payload` | `hd-payload` |
| `heimdall-payload-from` | `hd-from` |
| `heimdall-payload-ref` | `hd-ref` |
| `heimdall-prevent-default` | `hd-prevent-default` |
| `heimdall-ignore` | `hd-ignore` |
| `heimdall-scope` | `hd-scope` |
| `heimdall-sync` | `hd-sync` |
| `heimdall-sync-group` | `hd-sync-group` |
| `heimdall-debounce` | `hd-debounce` |
| `heimdall-key` | `hd-key` |
| `heimdall-hover-delay` | `hd-hover-delay` |
| `heimdall-visible-once` | `hd-visible-once` |
| `heimdall-scroll-threshold` | `hd-scroll-threshold` |
| `heimdall-poll` | `hd-poll` |
| `heimdall-sse` | `hd-sse` |
| `heimdall-sse-topic` | `hd-sse-topic` |
| `heimdall-sse-target` | `hd-sse-target` |
| `heimdall-sse-swap` | `hd-sse-swap` |
| `heimdall-sse-event` | `hd-sse-event` |
| `heimdall-sse-disable` | `hd-sse-disable` |
| `heimdall-time` | `hd-time` |
| `heimdall-time-format` | `hd-time-format` |
| `data-heimdall-state` | `data-hd-state` |
| `data-heimdall-state-*` | `data-hd-state-*` |

For example, `data-hd-state-cart` provides `closest-state:cart`. Prefix mappings preserve the key suffix.
Action IDs and values such as `closest-form`, `replace`, and `inner` keep their existing meanings.

These mappings apply to normal element behavior. Response instructions (`invocation`, `mutation`,
`history`, `storage`, `redirect`, `abort`, `javascript`, and `heimdall-command`) continue using canonical
attributes. Browser-owned attributes such as `disabled`, `lang`, and `commandfor` retain native HTML
semantics. C# rendering helpers continue emitting the original names.

## Application aliases

Applications can register aliases with or without the default shortcuts plugin:

```javascript
const undo = Heimdall.attributes.alias({
    "heimdall-content-click": ["team-click", "on-click"],
    "heimdall-payload-from": "team-from",
    "data-heimdall-state-*": "data-team-state-*"
});

// Undo only these registrations; repeated calls are harmless.
undo();
```

Registrations are synchronous and atomic. Unknown originals, invalid names, collisions with originals,
and overlapping prefix mappings throw without applying part of a batch. Names use lowercase letters,
digits, underscores, and hyphens, starting with a letter. Browser event-handler, ARIA, and reserved native
attribute names cannot be claimed. Prefix mappings must end in `-*`.

An existing alias can be explicitly reassigned to another registered attribute:

```javascript
const undoOverride = Heimdall.attributes.alias({
    "heimdall-content-submit": "team-action"
}, { replace: true });
```

Undo restores the previous live mapping. Original names always keep their identity. Multiple providers
can register the same alias for the same original; disposing one provider preserves the others.

Use the public catalog and readers for integrations:

```javascript
Heimdall.attributes.list(); // Frozen snapshots: name, owner, category, kind, aliases.
Heimdall.attributes.names("heimdall-content-click"); // Original followed by active aliases.
Heimdall.attributes.get(element, "heimdall-content-target");
Heimdall.attributes.has(element, "data-heimdall-state-cart");
document.querySelectorAll(Heimdall.attributes.selector("heimdall-content-click"));
```

Adding, reassigning, or removing aliases reconciles existing elements, even when DOM observation is
disabled. Unchanged load triggers do not fire again; polling and visibility subscriptions retain their
identity when their effective settings are unchanged. Losing an alias tears down the corresponding
polling, visibility, plugin-trigger, or declarative SSE subscription. Submitted requests retain their
existing request lifecycle and payload rules. Live queued state sources are read through the registry.
Pending input, change, and hover delays skip invocation if their trigger is no longer recognized.

To remove the default preset:

```javascript
Heimdall.plugins.remove("hd.shortcuts");
```

## Plugin attributes

Plugin trigger definitions use the same registry as built-ins. The generated original attribute is
`heimdall-content-{qualified-trigger-name-with-hyphens}` unless `attribute` supplies another original.
Plugins can declare aliases alongside the trigger:

```javascript
Heimdall.use({
    id: "company.editor",
    pluginApiVersion: 1,
    install(api) {
        api.triggers.register({
            name: "commit",
            events: ["company-editor-commit"],
            aliases: ["hd-commit"]
        });
        api.attributes.alias({ "heimdall-payload-from": "editor-from" });
    }
});
```

The default original and `hd-commit` share trigger discovery, scope/ignore rules, debounce, payloads,
targets, security, synchronization, and the normal request pipeline. Adding several spellings to one
element does not create duplicate trigger subscriptions.

Custom behaviors can use `api.attributes.get`, `has`, and `selector` to consume registered names.
Their `observedAttributes` signatures also resolve canonical framework attributes through the registry.

Applications can add aliases for a plugin attribute after that plugin registers it, through the same
`Heimdall.attributes.alias` API. The optional `hd.shortcuts` preset covers built-ins; plugin authors
choose their own shortcuts to avoid collisions between unrelated plugins.

`api.attributes.alias` is restricted to synchronous installation and automatically owns its mappings.
Failed installation rolls back trigger definitions and aliases together. Removing a plugin releases its
definitions and mappings, including aliases applications added for those definitions. Removing a
shortcut-provider plugin preserves mappings independently registered by other providers.
