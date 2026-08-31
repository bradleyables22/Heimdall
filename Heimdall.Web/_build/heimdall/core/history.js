const HISTORY_STATE_KEY = "__heimdall_history_v1";

export function createHistoryRuntime({ global, emitLifecycle, dbg }) {
    function currentUrl() {
        return `${global.location.pathname}${global.location.search}${global.location.hash}`;
    }

    function normalizeMode(value) {
        const mode = String(value || "").trim().toLowerCase();
        if (mode !== "push" && mode !== "replace")
            throw new Error(`Unsupported Heimdall history mode '${mode || "(empty)"}'.`);
        return mode;
    }

    function normalizeUrl(value) {
        const raw = String(value || "").trim();
        if (!raw)
            throw new Error("Heimdall history URL is required.");
        if (raw.includes("\\"))
            throw new Error("Heimdall history URLs cannot contain backslashes.");
        if (raw.startsWith("//"))
            throw new Error("Protocol-relative Heimdall history URLs are not allowed.");

        let candidate;
        if (raw.startsWith("?") || raw.startsWith("#")) {
            candidate = new URL(raw, global.location.href);
        } else if (/^[a-z][a-z\d+.-]*:/i.test(raw)) {
            candidate = new URL(raw);
        } else {
            candidate = new URL(raw.startsWith("/") ? raw : `/${raw}`, global.location.origin);
        }

        if (candidate.origin !== global.location.origin)
            throw new Error("Heimdall history URLs must be same-origin.");

        return `${candidate.pathname}${candidate.search}${candidate.hash}`;
    }

    function markState(value) {
        if (value && typeof value === "object" && value[HISTORY_STATE_KEY] === true)
            return value;

        if (value && Object.prototype.toString.call(value) === "[object Object]") {
            return Object.assign({}, value, { [HISTORY_STATE_KEY]: true });
        }

        return {
            [HISTORY_STATE_KEY]: true,
            previousState: value == null ? null : value
        };
    }

    function emitError(command, sourceElement, context, error) {
        const detail = {
            mode: command && command.mode != null ? String(command.mode) : null,
            url: command && command.url != null ? String(command.url) : null,
            error,
            sourceElement: sourceElement || null,
            requestContext: context && context.requestContext ? context.requestContext : null
        };
        emitLifecycle(sourceElement, "heimdall:history-error", detail);
        dbg("history directive ignored", detail);
    }

    function apply(command, sourceElement, context) {
        if (!command)
            return { applied: false, cancelled: false, mode: null, url: null, error: null };

        if (command.error) {
            const error = new Error(command.error);
            emitError(command, sourceElement, context, error);
            return { applied: false, cancelled: false, mode: null, url: null, error };
        }

        const detail = {
            mode: command.mode,
            url: command.url,
            sourceElement: sourceElement || null,
            requestContext: context && context.requestContext ? context.requestContext : null
        };

        if (!emitLifecycle(sourceElement, "heimdall:history-before", detail, { cancelable: true }))
            return { applied: false, cancelled: true, mode: detail.mode, url: detail.url, error: null };

        try {
            const mode = normalizeMode(detail.mode);
            const url = normalizeUrl(detail.url);

            if (mode === "push") {
                global.history.replaceState(markState(global.history.state), "", currentUrl());
                global.history.pushState(markState(null), "", url);
            } else {
                global.history.replaceState(markState(global.history.state), "", url);
            }

            const afterDetail = Object.assign({}, detail, { mode, url });
            emitLifecycle(sourceElement, "heimdall:history-after", afterDetail);
            return { applied: true, cancelled: false, mode, url, error: null };
        } catch (error) {
            emitError(detail, sourceElement, context, error);
            return { applied: false, cancelled: false, mode: null, url: null, error };
        }
    }

    global.addEventListener("popstate", event => {
        if (!event.state || event.state[HISTORY_STATE_KEY] !== true)
            return;

        const detail = { url: currentUrl(), state: event.state };
        if (!emitLifecycle(global.document, "heimdall:history-pop", detail, { cancelable: true }))
            return;

        global.location.reload();
    });

    return { apply, normalizeUrl };
}
