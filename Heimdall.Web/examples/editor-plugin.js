/** @type {import("../wwwroot/heimdall-plugins").HeimdallPlugin<{ commitKey?: string }>} */
const editorPlugin = {
    id: "company.editor",
    version: "1.0.0",
    pluginApiVersion: 1,

    install(api, options = {}) {
        const commitKey = options.commitKey || "Enter";

        api.behaviors.register({
            name: "keyboard",
            selector: "textarea[data-company-editor]",
            mount(element, { signal }) {
                element.addEventListener("keydown", event => {
                    if (!(event instanceof KeyboardEvent) || event.key !== commitKey || !event.ctrlKey)
                        return;
                    event.preventDefault();
                    element.dispatchEvent(new CustomEvent("company-editor-commit", { bubbles: true }));
                }, { signal });
            }
        });

        api.triggers.register({
            name: "commit",
            events: ["company-editor-commit"]
        });

        api.payloads.register({
            name: "document",
            read({ element }) {
                if (!(element instanceof HTMLTextAreaElement))
                    throw new TypeError("The editor payload requires a textarea.");
                return { text: element.value };
            }
        });

        api.commands.register({
            name: "set-text",
            /** @returns {args is { text: string }} */
            validate: args => typeof args === "object" && args !== null &&
                "text" in args && typeof args.text === "string",
            handle({ element, args }) {
                if (!(element instanceof HTMLTextAreaElement))
                    throw new TypeError("The editor command requires a textarea target.");
                element.value = args.text;
            }
        });
    }
};

export default editorPlugin;
