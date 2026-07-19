export function createDiagnostics(getConfig) {
    function emit(name, detail) {
        try {
            document.dispatchEvent(new CustomEvent(name, { detail }));
        } catch {
            // ignore
        }
    }

    function emitLifecycle(source, name, detail, options) {
        options = options || {};

        try {
            const sourceIsConnected = source && source.isConnected !== false && typeof source.dispatchEvent === "function";
            const target = sourceIsConnected ? source : document;
            const event = new CustomEvent(name, {
                detail,
                bubbles: target !== document,
                composed: target !== document,
                cancelable: !!options.cancelable
            });

            return target.dispatchEvent(event);
        } catch {
            return true;
        }
    }

    function dbg(...args) {
        const config = getConfig();
        if (config && config.debug) {
            // eslint-disable-next-line no-console
            console.debug(`[Heimdall]`, ...args);
        }
    }

    return {
        emit,
        emitLifecycle,
        dbg
    };
}
