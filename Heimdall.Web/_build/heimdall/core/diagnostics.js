export function createDiagnostics(getConfig) {
    function emit(name, detail) {
        try {
            document.dispatchEvent(new CustomEvent(name, { detail }));
        } catch {
            // ignore
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
        dbg
    };
}
