export const REQUEST_HEADERS_FAILED_CODE = "request-headers-failed";

export function createRequestHeadersRuntime({ global, getConfig }) {
    function abortError() {
        if (typeof global.DOMException === "function")
            return new global.DOMException("The request was aborted.", "AbortError");

        const error = new Error("The request was aborted.");
        error.name = "AbortError";
        return error;
    }

    function waitForProvider(value, signal) {
        const promise = Promise.resolve(value);
        if (!signal || typeof signal.addEventListener !== "function")
            return promise;
        if (signal.aborted)
            return Promise.reject(abortError());

        return new Promise((resolve, reject) => {
            let settled = false;
            const onAbort = () => {
                if (settled)
                    return;
                settled = true;
                reject(abortError());
            };

            signal.addEventListener("abort", onAbort, { once: true });
            promise.then(
                result => {
                    if (settled)
                        return;
                    settled = true;
                    signal.removeEventListener("abort", onAbort);
                    resolve(result);
                },
                error => {
                    if (settled)
                        return;
                    settled = true;
                    signal.removeEventListener("abort", onAbort);
                    reject(error);
                }
            );
        });
    }

    function sourceEntries(source) {
        if (source == null)
            return [];

        if (typeof global.Headers === "function" && source instanceof global.Headers)
            return Array.from(source.entries());

        if (Array.isArray(source))
            return source;

        if (typeof source === "object")
            return Object.entries(source);

        throw new TypeError("requestHeaders must return a Headers instance, header pairs, an object, or null.");
    }

    function validatedEntry(entry) {
        if (!Array.isArray(entry) || entry.length !== 2)
            throw new TypeError("requestHeaders header pairs must contain exactly a name and value.");

        const name = String(entry[0]);
        const value = entry[1];

        if (typeof global.Headers !== "function")
            return [name, String(value)];

        const validation = new global.Headers([[name, value]]);
        return [name, validation.get(name) || ""];
    }

    function setHeader(target, name, value) {
        const lowerName = name.toLowerCase();
        for (const existing of Object.keys(target)) {
            if (existing.toLowerCase() === lowerName)
                delete target[existing];
        }

        target[name] = value;
    }

    function merge(target, source) {
        for (const entry of sourceEntries(source)) {
            const [name, value] = validatedEntry(entry);
            setHeader(target, name, value);
        }

        return target;
    }

    async function resolve(context, initialHeaders) {
        const headers = merge({}, initialHeaders);
        const configured = getConfig()?.requestHeaders;
        if (configured == null)
            return headers;

        try {
            const providerContext = {
                ...context,
                headers
            };
            const provided = typeof configured === "function"
                ? await waitForProvider(configured(providerContext), context?.signal)
                : configured;

            // Normalize mutations made through context.headers before applying
            // returned values. Returned headers win when both paths set a name.
            const normalized = merge({}, headers);
            merge(normalized, provided);
            return normalized;
        } catch (cause) {
            const kind = context?.kind || "request";
            const message = cause && cause.message
                ? cause.message
                : String(cause || "Unknown request header provider failure.");
            const error = new Error(`Heimdall requestHeaders failed for ${kind}: ${message}`);
            error.name = "HeimdallRequestHeadersError";
            error.code = REQUEST_HEADERS_FAILED_CODE;
            error.requestKind = kind;
            error.cause = cause;
            throw error;
        }
    }

    return {
        merge,
        resolve
    };
}
