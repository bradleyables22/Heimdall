import testPlugin from "/_content/Heimdall.E2E.TestPlugin/js/test-plugin.js";

export const pluginOptions = Object.freeze({ instancePrefix: "host", initializeDelayMs: 20 });
export { testPlugin };

document.addEventListener("e2e-plugin:event", event => {
    if (!(event instanceof CustomEvent)) return;
    const detail = event.detail;
    const lifecycle = document.querySelector("#plugin-lifecycle");
    if (lifecycle instanceof HTMLElement && ["mounted", "disposed", "updated", "unsubscribed"].includes(detail.phase)) {
        lifecycle.dataset[detail.phase] = String(Number(lifecycle.dataset[detail.phase] || 0) + 1);
        lifecycle.textContent = ["mounted", "disposed", "updated", "unsubscribed"]
            .map(phase => `${phase}: ${lifecycle.dataset[phase]}`).join(" | ");
    }
    const log = document.querySelector("#plugin-command-log");
    if (log && ["command", "record", "removed"].includes(detail.phase)) {
        const entry = document.createElement("li");
        for (const key of ["phase", "message", "text", "timing", "origin", "ready"])
            entry.dataset[key] = String(detail[key] ?? "");
        entry.textContent = JSON.stringify(detail);
        log.append(entry);
    }
});

document.addEventListener("heimdall:plugin-error", event => {
    const output = document.querySelector("#plugin-errors");
    if (!(output instanceof HTMLElement)) return;
    output.dataset.count = String(Number(output.dataset.count || 0) + 1);
    output.textContent = `${event.detail.pluginId || "unknown"}: ${event.detail.phase}/${event.detail.code}`;
});

document.addEventListener("heimdall:sse-open", event => {
    if (!(event instanceof CustomEvent)) return;
    const root = document.querySelector("#plugin-workbench");
    const stream = document.querySelector("#plugin-stream-host");
    if (stream instanceof HTMLElement && event.detail.topic === root?.getAttribute("data-plugin-topic"))
        stream.dataset.connection = "open";
});

Heimdall.use(testPlugin, pluginOptions);
