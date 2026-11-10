export function createHeimdallRuntime({
    global,
    attributes,
    reconcileAttributes,
    apiVersion,
    defaultBasePath,
    defaultContentEndpoint,
    defaultCsrfEndpoint,
    defaultBifrostTokenEndpoint,
    defaultBifrostEndpoint,
    invoke,
    boot,
    onReady,
    clearCsrfToken,
    plugins,
    sseConnect,
    sseDisconnect,
    sseDisconnectAll,
    handlers,
    installPageLifecycle,
    installSseSweeper,
    dbg,
    onRuntimeCreated
}) {
    let started = false;
    function installObserver() {
        if (!Heimdall.config.observeDom)
            return;
        if (document.__heimdallObserverInstalled)
            return;
        document.__heimdallObserverInstalled = true;

        let pending = new Set();
        let scheduled = false;

        function flush() {
            scheduled = false;
            const nodes = Array.from(pending);
            pending.clear();
            for (const node of nodes)
                boot(node);
        }

        function scheduleFlush() {
            if (scheduled)
                return;
            scheduled = true;
            Promise.resolve().then(flush);
        }

        const obs = new MutationObserver((mutations) => {
            plugins.observe(mutations);
            for (const m of mutations) {
                if (m.type === "attributes") {
                    if (m.attributeName !== "lang" && !attributes.recognizes(m.attributeName)) continue;
                    if (m.target && m.target.nodeType === 1)
                        pending.add(m.target);
                    continue;
                }

                for (const n of m.addedNodes) {
                    if (!n || n.nodeType !== 1)
                        continue;
                    pending.add(n);
                }
            }
            if (pending.size) scheduleFlush();
        });

        function observe() {
            if (!Heimdall.config.observeDom) return;
            obs.observe(document.documentElement, {
                childList: true, subtree: true, attributes: true
            });
        }
        observe();
        plugins.onRegistryChanged(observe);
        Heimdall._observer = obs;

        dbg("MutationObserver installed");
    }

    const Heimdall = {
        apiVersion,

        invoke,
        boot,
        onReady,
        clearCsrfToken,
        attributes: attributes.publicApi,
        pluginApiVersion: plugins.pluginApiVersion,
        use(plugin, options) {
            plugins.use(plugin, options);
            return Heimdall;
        },
        plugins: Object.freeze({ list: plugins.list, remove: plugins.remove }),
        dispose: plugins.dispose,

        sse: {
            connect: sseConnect,
            disconnect: sseDisconnect,
            disconnectAll: sseDisconnectAll
        },

        _observer: null,

        config: {
            basePath: defaultBasePath,
            apiVersion,

            endpoints: {
                contentInvocations: defaultContentEndpoint,
                csrf: defaultCsrfEndpoint,
                bifrostToken: defaultBifrostTokenEndpoint,
                bifrost: defaultBifrostEndpoint
            },

            observeDom: true,
            debug: false,
            authReturnUrlParameter: "ReturnUrl",
            requestHeaders: null,
            credentials: "same-origin",
            antiforgery: true,
            clientInfo: false,
            clientInfoMaxAgeMs: 60000,
            clientInfoLimits: {
                maxKeysPerArea: 16,
                maxKeyLength: 128,
                maxValueLength: 1024,
                maxStorageChars: 2048,
                maxExtensionEntries: 16,
                maxExtensionChars: 1024,
                maxExtensionDepth: 4
            },

            inputDebounceMs: 250,
            hoverDelayMs: 150,
            scrollThresholdPx: 120,
            scrollMinIntervalMs: 250,
            requestSync: "parallel",
            requestTimeoutMs: 0,
            pluginMountTimeoutMs: 10000,
            pluginCommandTimeoutMs: 10000,
            pluginCommandMaxArgsChars: 65536,
            pluginCommandMaxCount: 256,
            pluginSseQueueLimit: 128,

            // NOTE: visibleRootMargin and visibleThreshold are read once when
            // the IntersectionObserver is first created. Set these values before
            // any heimdall-content-visible element is booted (i.e. before DOMContentLoaded).
            visibleRootMargin: "0px",
            visibleThreshold: 0,

            oobEnabled: true,

            sseDefaultSwap: "none",
            sseEventName: "heimdall",
            sseReconnectDelayMs: 250,
            sseReconnectMaxDelayMs: 10000,
            sseReconnectBackoffFactor: 2,
            sseSweepIntervalMs: 5000,
            ssePauseWhenHidden: false
        }
    };

    // Keep the old endpoint property as a source-compatible alias. Both names
    // always point at the same configured endpoint.
    Object.defineProperty(Heimdall.config.endpoints, "contentActions", {
        configurable: true,
        enumerable: true,
        get() {
            return Heimdall.config.endpoints.contentInvocations;
        },
        set(value) {
            Heimdall.config.endpoints.contentInvocations = value;
        }
    });

    if (typeof onRuntimeCreated === "function")
        onRuntimeCreated(Heimdall);

    global.Heimdall = Heimdall;

    attributes.onChange(() => {
        if (!started) return;
        reconcileAttributes();
    });

    onReady(() => {
        if (!document.__heimdallDelegatesInstalled) {
            document.__heimdallDelegatesInstalled = true;

            document.addEventListener("click", handlers.handleClick, true);
            document.addEventListener("change", handlers.handleChange, false);
            document.addEventListener("input", handlers.handleInput, false);
            document.addEventListener("submit", handlers.handleSubmit, false);
            document.addEventListener("keydown", handlers.handleKeydown, false);
            document.addEventListener("focusout", handlers.handleFocusOut, false);
            document.addEventListener("mouseover", handlers.handleMouseOver, false);
            document.addEventListener("mouseout", handlers.handleMouseOut, false);
        }

        plugins.start();
        started = true;
        boot(document);
        installObserver();
        installPageLifecycle();
        installSseSweeper();

        if (global.Blazor && typeof global.Blazor.addEventListener === "function") {
            global.Blazor.addEventListener("enhancedload", () => {
                boot(document);
            });
        }
    });

    return Heimdall;
}
