/** @typedef {import("../../../Heimdall.Web/wwwroot/heimdall-plugins").CommandContext<{ text: string, delayMs?: number }>} TextCommand */

/** @param {number} milliseconds @param {AbortSignal} signal */
function delay(milliseconds, signal) {
    return new Promise((resolve, reject) => {
        const abort = () => { clearTimeout(timer); reject(new DOMException("Cancelled", "AbortError")); };
        const timer = setTimeout(() => { signal.removeEventListener("abort", abort); resolve(undefined); }, milliseconds);
        if (signal.aborted) abort();
        else signal.addEventListener("abort", abort, { once: true });
    });
}

/** @param {string} phase @param {Element | null} element @param {Record<string, unknown>} [values] */
function notify(phase, element, values = {}) {
    document.dispatchEvent(new CustomEvent("e2e-plugin:event", {
        detail: { phase, elementId: element?.id || null, ...values }
    }));
}

/** @type {import("../../../Heimdall.Web/wwwroot/heimdall-plugins").HeimdallPlugin<{ instancePrefix: string, initializeDelayMs: number }>} */
const testPlugin = {
    id: "e2e.test-plugin",
    version: "1.0.0",
    pluginApiVersion: 1,

    install(api, options) {
        if (!options || !options.instancePrefix || !Number.isFinite(options.initializeDelayMs) ||
            options.initializeDelayMs < 0 || options.initializeDelayMs > 1000)
            throw new TypeError("The test plugin requires explicit host options.");

        let nextInstance = 0;
        /** @type {WeakMap<HTMLTextAreaElement, { id: string }>} */
        const instances = new WeakMap();

        api.behaviors.register({
            name: "editor",
            selector: "textarea[data-e2e-plugin-editor]",
            observedAttributes: ["data-plugin-mode"],
            async mount(element, { signal }) {
                if (!(element instanceof HTMLTextAreaElement)) throw new TypeError("An editor must be a textarea.");
                await delay(options.initializeDelayMs, signal);
                const instance = { id: `${options.instancePrefix}-${++nextInstance}` };
                instances.set(element, instance);
                element.dataset.pluginInstance = instance.id;
                element.dataset.pluginReady = "true";
                element.addEventListener("keydown", event => {
                    if (event.key !== "Enter" || !event.ctrlKey) return;
                    event.preventDefault();
                    element.dispatchEvent(new CustomEvent("e2e-plugin:commit", { bubbles: true }));
                }, { signal });
                notify("mounted", element, { instance: instance.id });
                return {
                    update() { notify("updated", element, { mode: element.dataset.pluginMode }); },
                    dispose() {
                        instances.delete(element);
                        delete element.dataset.pluginReady;
                        notify("disposed", element, { instance: instance.id });
                    }
                };
            }
        });

        api.triggers.register({ name: "commit", events: ["e2e-plugin:commit"] });
        api.triggers.register({
            name: "notify",
            subscribe({ element, emit, signal }) {
                element.addEventListener("click", event => { void emit(event); }, { signal });
                return () => notify("unsubscribed", element);
            }
        });

        api.payloads.register({
            name: "document",
            read({ element }) {
                const root = element.closest("#plugin-workbench");
                const editor = element instanceof HTMLTextAreaElement ? element : root?.querySelector("#plugin-editor");
                if (!(editor instanceof HTMLTextAreaElement)) throw new Error("The editor is missing.");
                const instance = instances.get(editor);
                if (!instance) throw new Error("The editor was read before initialization.");
                return {
                    text: editor.value,
                    clientInstance: instance.id,
                    mode: editor.dataset.pluginMode,
                    label: options.instancePrefix,
                    topic: root?.getAttribute("data-plugin-topic") || "",
                    delayMs: Number(editor.dataset.requestDelay || 0)
                };
            }
        });

        api.commands.register({
            name: "set-text",
            /** @returns {args is { text: string, delayMs?: number }} */
            validate: args => typeof args === "object" && args !== null && "text" in args && typeof args.text === "string",
            /** @param {TextCommand} context */
            async handle({ element, args, signal, origin }) {
                if (!(element instanceof HTMLTextAreaElement) || !instances.has(element))
                    throw new Error("A command reached an uninitialized editor.");
                if (args.delayMs) await delay(args.delayMs, signal);
                if (signal.aborted) return;
                element.value = args.text;
                notify("command", element, { text: args.text, origin, ready: element.dataset.pluginReady === "true" });
            }
        });
        api.commands.register({
            name: "record",
            /** @returns {args is { message: string }} */
            validate: args => typeof args === "object" && args !== null && "message" in args && typeof args.message === "string",
            handle({ args, timing, origin }) {
                const editor = document.querySelector("#plugin-editor");
                notify("record", editor, {
                    message: args.message, timing, origin,
                    text: editor instanceof HTMLTextAreaElement ? editor.value : null
                });
            }
        });
        api.commands.register({ name: "fail", handle() { throw new Error("Expected test-plugin command failure."); } });
        api.onDispose(() => notify("removed", null));
    }
};

export default testPlugin;
