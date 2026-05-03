    // ============================================================
    // Boot helpers
    // ------------------------------------------------------------
    // FIX (all boot functions): querySelectorAll only matches DESCENDANTS.
    // When a swapped-in element IS the trigger element (not a container),
    // boot(thatElement) would silently skip it. We now check the root
    // element itself before descending into its subtree.
    // ============================================================

    function matchesTriggerAttr(el, attr) {
        return isElement(el) && el.hasAttribute(attr);
    }

    function bootLoads(root) {
        const scope = isElement(root) ? root : document;
        const candidates = [];

        // Check root itself
        if (isElement(root) && matchesTriggerAttr(root, "heimdall-content-load"))
            candidates.push(root);

        // Descendants
        for (const el of scope.querySelectorAll("[heimdall-content-load]"))
            candidates.push(el);

        for (const el of candidates) {
            if (el.__heimdallLoaded)
                continue;
            el.__heimdallLoaded = true;

            const actionId = getAttr(el, "heimdall-content-load");
            if (!actionId)
                continue;

            runActionFromElement(el, actionId, "load").catch(() => { /* logged */ });
        }
    }

    let _visibleObserver = null;

    function ensureVisibleObserver() {
        if (_visibleObserver)
            return _visibleObserver;

        if (!("IntersectionObserver" in global)) {
            _visibleObserver = { observe() { }, unobserve() { } };
            return _visibleObserver;
        }

        _visibleObserver = new IntersectionObserver((entries) => {
            for (const entry of entries) {
                if (!entry.isIntersecting)
                    continue;

                const el = entry.target;
                const actionId = getAttr(el, "heimdall-content-visible");
                if (!actionId)
                    continue;

                const once = truthyAttr(el, "heimdall-visible-once", true);
                if (once) {
                    try {
                        _visibleObserver.unobserve(el);
                    }
                    catch { /* ignore */ }
                }

                runActionFromElement(el, actionId, "visible").catch(() => { /* logged */ });
            }
        }, {
            root: null,
            rootMargin: Heimdall.config.visibleRootMargin || "0px",
            threshold: Heimdall.config.visibleThreshold || 0
        });

        return _visibleObserver;
    }

    function bootVisible(root) {
        const scope = isElement(root) ? root : document;
        const obs = ensureVisibleObserver();
        const candidates = [];

        // FIX: check root element itself � querySelectorAll misses it
        if (isElement(root) && matchesTriggerAttr(root, "heimdall-content-visible"))
            candidates.push(root);

        // Descendants
        for (const el of scope.querySelectorAll("[heimdall-content-visible]"))
            candidates.push(el);

        for (const el of candidates) {
            if (el.__heimdallVisibleBound)
                continue;
            el.__heimdallVisibleBound = true;

            try {
                obs.observe(el);
            }
            catch { /* ignore */ }
        }
    }

    const _scrollState = new WeakMap();

    function isNearScrollEnd(el, thresholdPx) {
        const target = (el === document.body || el === document.documentElement)
            ? (document.scrollingElement || document.documentElement)
            : el;

        if (!target)
            return false;

        const scrollTop = target.scrollTop;
        const clientHeight = target.clientHeight;
        const scrollHeight = target.scrollHeight;

        return (scrollTop + clientHeight) >= (scrollHeight - thresholdPx);
    }

    function attachScroll(el) {
        if (el.__heimdallScrollBound)
            return;
        el.__heimdallScrollBound = true;

        const handler = () => {
            const state = _scrollState.get(el) || { ticking: false, lastFire: 0 };
            if (state.ticking)
                return;

            state.ticking = true;
            _scrollState.set(el, state);

            requestAnimationFrame(() => {
                state.ticking = false;

                const threshold = intAttr(el, "heimdall-scroll-threshold", Heimdall.config.scrollThresholdPx || 120);
                const minInterval = Heimdall.config.scrollMinIntervalMs || 250;

                if (!isNearScrollEnd(el, threshold))
                    return;

                const now = Date.now();
                if ((now - state.lastFire) < minInterval)
                    return;
                state.lastFire = now;

                const actionId = getAttr(el, "heimdall-content-scroll");
                if (!actionId)
                    return;

                runActionFromElement(el, actionId, "scroll").catch(() => { /* logged */ });
            });
        };

        el.addEventListener("scroll", handler, { passive: true });
    }

    function bootScroll(root) {
        const scope = isElement(root) ? root : document;

        // FIX: check root element itself
        if (isElement(root) && matchesTriggerAttr(root, "heimdall-content-scroll"))
            attachScroll(root);

        for (const el of scope.querySelectorAll("[heimdall-content-scroll]"))
            attachScroll(el);
    }

    const _pollState = new WeakMap();

    function attachPoll(el) {
        if (el.__heimdallPollBound)
            return;
        el.__heimdallPollBound = true;

        const intervalMs = intAttr(el, "heimdall-poll", 0);
        if (!intervalMs || intervalMs <= 0)
            return;

        const actionId = getAttr(el, "heimdall-content-load");
        if (!actionId) {
            // Always warn � misconfigured polling is a silent no-op and hard to debug.
            // eslint-disable-next-line no-console
            console.warn(`[Heimdall] heimdall-poll set but no heimdall-content-load found on element.`, el);
            return;
        }

        const state = { timerId: null, inFlight: false };
        _pollState.set(el, state);

        const tick = async () => {
            if (!el.isConnected) {
                stopPoll(el);
                return;
            }
            if (document.hidden)
                return;
            if (state.inFlight)
                return;

            state.inFlight = true;
            try {
                await runActionFromElement(el, actionId, "load", { reason: "poll" });
            }
            finally {
                state.inFlight = false;
            }
        };

        const schedule = () => {
            if (!el.isConnected) {
                stopPoll(el);
                return;
            }

            const st = _pollState.get(el);
            if (!st)
                return;

            clearTimeout(st.timerId);
            st.timerId = setTimeout(async () => {
                try {
                    await tick();
                }
                catch { /* runActionFromElement logs */ }
                finally {
                    schedule();
                }
            }, intervalMs);
        };

        schedule();
    }

    function stopPoll(el) {
        const st = _pollState.get(el);
        if (!st)
            return;
        clearTimeout(st.timerId);
        _pollState.delete(el);
        el.__heimdallPollBound = false;
    }

    function bootPoll(root) {
        const scope = isElement(root) ? root : document;

        // FIX: check root element itself
        if (isElement(root) && matchesTriggerAttr(root, "heimdall-poll"))
            attachPoll(root);

        for (const el of scope.querySelectorAll("[heimdall-poll]"))
            attachPoll(el);
    }

