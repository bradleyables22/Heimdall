export function createJsInvokeVoidRuntime({ global, emit, dbg, getConfig }) {
    const validSegment = /^[A-Za-z_$][A-Za-z0-9_$]*$/;

    function normalizeTiming(value) {
        return String(value || "after").toLowerCase().trim() === "before"
            ? "before"
            : "after";
    }

    function parseArgs(argsJson) {
        if (argsJson == null || String(argsJson).trim() === "")
            return [];

        const parsed = JSON.parse(argsJson);
        if (!Array.isArray(parsed))
            throw new Error("Heimdall JavaScript invocation args must be a JSON array.");

        return parsed;
    }

    function resolveRoot(name) {
        switch (name) {
            case "window":
                return global;
            case "globalThis":
                return global.globalThis || global;
            case "document":
                return global.document;
            default:
                return undefined;
        }
    }

    function resolveFunction(functionPath) {
        const path = String(functionPath || "").trim();
        const parts = path.split(".").filter(Boolean);

        if (parts.length < 2)
            throw new Error("Heimdall JavaScript invocation requires an explicitly rooted function path.");

        const root = resolveRoot(parts[0]);
        if (!root)
            throw new Error("Heimdall JavaScript invocation paths must start with window., globalThis., or document.");

        for (const part of parts) {
            if (!validSegment.test(part))
                throw new Error("Heimdall JavaScript invocation paths support dotted property access only.");
        }

        let owner = root;
        for (let i = 1; i < parts.length - 1; i++) {
            owner = owner == null ? undefined : owner[parts[i]];
        }

        const functionName = parts[parts.length - 1];
        const fn = owner == null ? undefined : owner[functionName];
        if (typeof fn !== "function")
            throw new Error(`Heimdall JavaScript function '${path}' was not found.`);

        return { owner, fn, path };
    }

    function emitError(directive, error, context) {
        const detail = {
            functionPath: directive && directive.functionPath ? directive.functionPath : null,
            timing: normalizeTiming(directive && directive.timing),
            args: directive && Array.isArray(directive.args) ? directive.args : null,
            sourceEl: directive && directive.sourceEl ? directive.sourceEl : null,
            context: context || null,
            error
        };

        emit("heimdall:javascript-error", detail);

        if (getConfig && getConfig().debug) {
            // eslint-disable-next-line no-console
            console.error("[Heimdall] JavaScript invocation failed", detail);
        }
    }

    function invokeDirective(directive, context) {
        const invocation = Object.assign({}, directive || {});

        try {
            invocation.timing = normalizeTiming(invocation.timing);
            invocation.args = Array.isArray(invocation.args)
                ? invocation.args
                : parseArgs(invocation.argsJson);

            const resolved = resolveFunction(invocation.functionPath);
            const result = resolved.fn.apply(resolved.owner, invocation.args);

            if (result && typeof result.then === "function") {
                result.then(null, error => {
                    emitError(invocation, error, context);
                });
            }

            dbg("js invoke void", {
                functionPath: resolved.path,
                timing: invocation.timing,
                args: invocation.args
            });

            return true;
        } catch (error) {
            emitError(invocation, error, context);
            return false;
        }
    }

    function invokeAll(directives, context) {
        if (!Array.isArray(directives) || directives.length === 0)
            return 0;

        let count = 0;
        for (const directive of directives) {
            if (invokeDirective(directive, context))
                count++;
        }

        return count;
    }

    return {
        invokeAll,
        normalizeTiming
    };
}
