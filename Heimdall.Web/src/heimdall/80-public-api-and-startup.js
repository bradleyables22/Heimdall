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
            for (const m of mutations) {
                for (const n of m.addedNodes) {
                    if (!n || n.nodeType !== 1)
                        continue;
                    pending.add(n);
                }
            }
            if (pending.size) scheduleFlush();
        });

        obs.observe(document.body, { childList: true, subtree: true });
        Heimdall._observer = obs;

        dbg("MutationObserver installed");
    }

    const Heimdall = {
        apiVersion: API_VERSION,

        invoke,
        boot,
        onReady,
        clearCsrfToken,

        sse: {
            connect: sseConnect,
            disconnect: sseDisconnect,
            disconnectAll: sseDisconnectAll
        },

        _observer: null,

        config: {
            basePath: DEFAULT_BASE_PATH,
            apiVersion: API_VERSION,

            endpoints: {
                contentActions: DEFAULT_CONTENT_ENDPOINT,
                csrf: DEFAULT_CSRF_ENDPOINT,
                bifrostToken: DEFAULT_BIFROST_TOKEN_ENDPOINT,
                bifrost: DEFAULT_BIFROST_ENDPOINT
            },

            observeDom: true,
            debug: false,

            inputDebounceMs: 250,
            hoverDelayMs: 150,
            scrollThresholdPx: 120,
            scrollMinIntervalMs: 250,

            // NOTE: visibleRootMargin and visibleThreshold are read once when
            // the IntersectionObserver is first created. Set these values before
            // any heimdall-content-visible element is booted (i.e. before DOMContentLoaded).
            visibleRootMargin: "0px",
            visibleThreshold: 0,

            oobEnabled: true,

            sseDefaultSwap: "none",
            sseEventName: "heimdall",
            sseSweepIntervalMs: 5000,
            ssePauseWhenHidden: false
        }
    };

    global.Heimdall = Heimdall;

    onReady(() => {
        if (!document.__heimdallDelegatesInstalled) {
            document.__heimdallDelegatesInstalled = true;

            document.addEventListener("click", handleClick, true);
            document.addEventListener("change", handleChange, false);
            document.addEventListener("input", handleInput, false);
            document.addEventListener("submit", handleSubmit, false);
            document.addEventListener("keydown", handleKeydown, false);
            document.addEventListener("focusout", handleFocusOut, false);
            document.addEventListener("mouseover", handleMouseOver, false);
            document.addEventListener("mouseout", handleMouseOut, false);
        }

        boot(document);
        installObserver();
        installSseSweeper();

        if (global.Blazor && typeof global.Blazor.addEventListener === "function") {
            global.Blazor.addEventListener("enhancedload", () => {
                boot(document);
            });
        }
    });

})(window);
