import type { HeimdallPlugin } from "../../wwwroot/heimdall-plugins";
import editorPlugin from "../../examples/editor-plugin.js";

Heimdall.use(editorPlugin, { commitKey: "Enter" });
// @ts-expect-error A consumer's options must conform to its plugin's declared contract.
Heimdall.use(editorPlugin, { commitKey: 123 });

const plugin: HeimdallPlugin = {
    id: "example.types",
    pluginApiVersion: 1,
    install(api) {
        api.triggers.register({ name: "commit", events: ["custom-commit"], aliases: ["hd-commit"] });
        api.attributes.alias({ "heimdall-content-click": ["team-click", "on-click"] });
        api.attributes.list().forEach(definition => definition.aliases.forEach(alias => alias.toUpperCase()));
        // @ts-expect-error Aliases must be attribute names, not booleans.
        api.triggers.register({ name: "invalid", events: ["invalid"], aliases: [true] });
        api.behaviors.register({
            name: "widget", selector: "[data-widget]",
            mount(element, { signal }) {
                element.addEventListener("change", () => {}, { signal });
                return { dispose() {} };
            }
        });
        api.payloads.register({ name: "value", read: ({ element }) => ({ id: element.id }) });
        api.commands.register<{ text: string }>({
            name: "update",
            validate: (args): args is { text: string } => typeof args === "object" && args !== null && "text" in args,
            handle({ args }) { args.text.toUpperCase(); }
        });
        document.addEventListener("heimdall:plugin-error", event => event.detail.code.toUpperCase());
        api.commands.register<{ text: string }>({
            name: "schema-validated",
            validate: args => Boolean(args),
            handle({ args }) { args.text.toUpperCase(); }
        });
    }
};
Heimdall.use(plugin);

const removeAliases: () => void = Heimdall.attributes.alias({
    "heimdall-content-submit": "hd-submit",
    "data-heimdall-state-*": "data-team-state-*"
}, { replace: true });
Heimdall.attributes.get(document.body, "heimdall-content-submit")?.toUpperCase();
Heimdall.attributes.has(document.body, "data-heimdall-state-cart");
document.querySelector(Heimdall.attributes.selector("heimdall-content-click"));
removeAliases();
// @ts-expect-error Alias maps contain strings or arrays of strings.
Heimdall.attributes.alias({ "heimdall-content-click": 123 });
