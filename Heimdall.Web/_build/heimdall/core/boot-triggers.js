import {
    getAttr,
    intAttr,
    isElement,
    truthyAttr
} from "./utils.js";

export function createBootTriggers({
    global,
    getConfig,
    runActionFromElement
}) {
    function matchesTriggerAttr(el, attr) {
        return isElement(el) && el.hasAttribute(attr);
    }

    function candidates(root, selector) {
        const result = [];
        const seen = new Set();
        const add = el => {
            if (!isElement(el) || seen.has(el))
                return;
            seen.add(el);
            result.push(el);
        };

        // Always reconcile the exact root. This tears behavior down when a
        // mutation removes its controlling attribute.
        add(root);

        const scope = isElement(root) ? root : document;
        for (const el of scope.querySelectorAll(selector))
            add(el);

        return result;
    }

    const _loadActions = new WeakMap();

    function reconcileLoad(el) {
        const actionId = (getAttr(el, "heimdall-content-load") || "").trim();
        const previous = _loadActions.get(el) || null;

        if (!actionId) {
            _loadActions.delete(el);
            el.__heimdallLoaded = false;
            return;
        }

        if (previous === actionId)
            return;

        _loadActions.set(el, actionId);
        el.__heimdallLoaded = true;
        runActionFromElement(el, actionId, "load").catch(() => { /* logged */ });
    }

    function bootLoads(root) {
        for (const el of candidates(root, "[heimdall-content-load]"))
            reconcileLoad(el);
    }

    let _visibleObserver = null;
    const _visibleStates = new WeakMap();

    function ensureVisibleObserver() {
        if (_visibleObserver)
            return _visibleObserver;

        if (!("IntersectionObserver" in global)) {
            _visibleObserver = { observe() { }, unobserve() { } };
            return _visibleObserver;
        }

        const config = getConfig();
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

                    const state = _visibleStates.get(el);
                    if (state)
                        state.observed = false;
                }

                runActionFromElement(el, actionId, "visible").catch(() => { /* logged */ });
            }
        }, {
            root: null,
            rootMargin: config.visibleRootMargin || "0px",
            threshold: config.visibleThreshold || 0
        });

        return _visibleObserver;
    }

    function stopVisible(el) {
        const state = _visibleStates.get(el);
        if (!state)
            return;
        try {
            ensureVisibleObserver().unobserve(el);
        }
        catch { /* ignore */ }
        _visibleStates.delete(el);
        el.__heimdallVisibleBound = false;
    }

    function reconcileVisible(el) {
        const actionId = (getAttr(el, "heimdall-content-visible") || "").trim();
        if (!actionId) {
            stopVisible(el);
            return;
        }

        const once = truthyAttr(el, "heimdall-visible-once", true);
        const previous = _visibleStates.get(el);
        if (previous && previous.actionId === actionId && previous.once === once)
            return;

        if (previous)
            stopVisible(el);

        _visibleStates.set(el, { actionId, once, observed: true });
        el.__heimdallVisibleBound = true;
        try {
            ensureVisibleObserver().observe(el);
        }
        catch { /* ignore */ }
    }

    function bootVisible(root) {
        for (const el of candidates(root, "[heimdall-content-visible]"))
            reconcileVisible(el);
    }

    const _scrollStates = new WeakMap();

    function isNearScrollEnd(el, thresholdPx) {
        const target = (el === document.body || el === document.documentElement)
            ? (document.scrollingElement || document.documentElement)
            : el;

        if (!target)
            return false;

        return (target.scrollTop + target.clientHeight) >= (target.scrollHeight - thresholdPx);
    }

    function stopScroll(el) {
        const state = _scrollStates.get(el);
        if (!state)
            return;
        try {
            el.removeEventListener("scroll", state.handler);
        }
        catch { /* ignore */ }
        _scrollStates.delete(el);
        el.__heimdallScrollBound = false;
    }

    function reconcileScroll(el) {
        const actionId = (getAttr(el, "heimdall-content-scroll") || "").trim();
        if (!actionId) {
            stopScroll(el);
            return;
        }

        const previous = _scrollStates.get(el);
        if (previous && previous.actionId === actionId)
            return;
        if (previous)
            stopScroll(el);

        const state = { actionId, ticking: false, lastFire: 0, handler: null };
        state.handler = () => {
            if (state.ticking)
                return;

            state.ticking = true;
            requestAnimationFrame(() => {
                state.ticking = false;

                const currentActionId = getAttr(el, "heimdall-content-scroll");
                if (!currentActionId)
                    return;

                const config = getConfig();
                const threshold = intAttr(el, "heimdall-scroll-threshold", config.scrollThresholdPx || 120);
                const minInterval = config.scrollMinIntervalMs || 250;
                if (!isNearScrollEnd(el, threshold))
                    return;

                const now = Date.now();
                if ((now - state.lastFire) < minInterval)
                    return;
                state.lastFire = now;

                runActionFromElement(el, currentActionId, "scroll").catch(() => { /* logged */ });
            });
        };

        _scrollStates.set(el, state);
        el.__heimdallScrollBound = true;
        el.addEventListener("scroll", state.handler, { passive: true });
    }

    function bootScroll(root) {
        for (const el of candidates(root, "[heimdall-content-scroll]"))
            reconcileScroll(el);
    }

    const _pollStates = new WeakMap();

    function stopPoll(el) {
        const state = _pollStates.get(el);
        if (!state)
            return;
        clearTimeout(state.timerId);
        _pollStates.delete(el);
        el.__heimdallPollBound = false;
    }

    function startPoll(el, actionId, intervalMs) {
        const state = { actionId, intervalMs, timerId: null, inFlight: false };
        _pollStates.set(el, state);
        el.__heimdallPollBound = true;

        const tick = async () => {
            if (!el.isConnected) {
                stopPoll(el);
                return;
            }
            if (document.hidden || state.inFlight)
                return;

            state.inFlight = true;
            try {
                await runActionFromElement(el, state.actionId, "load", { reason: "poll" });
            }
            finally {
                state.inFlight = false;
            }
        };

        const schedule = () => {
            if (!el.isConnected || _pollStates.get(el) !== state) {
                stopPoll(el);
                return;
            }

            clearTimeout(state.timerId);
            state.timerId = setTimeout(async () => {
                try {
                    await tick();
                }
                catch { /* runActionFromElement logs */ }
                finally {
                    if (_pollStates.get(el) === state)
                        schedule();
                }
            }, state.intervalMs);
        };

        schedule();
    }

    function reconcilePoll(el) {
        const intervalMs = intAttr(el, "heimdall-poll", 0);
        const actionId = (getAttr(el, "heimdall-content-load") || "").trim();
        const previous = _pollStates.get(el);

        if (!intervalMs || intervalMs <= 0 || !actionId) {
            if (intervalMs > 0 && !actionId) {
                // eslint-disable-next-line no-console
                console.warn(`[Heimdall] heimdall-poll set but no heimdall-content-load found on element.`, el);
            }
            stopPoll(el);
            return;
        }

        if (previous && previous.intervalMs === intervalMs && previous.actionId === actionId)
            return;

        if (previous)
            stopPoll(el);
        startPoll(el, actionId, intervalMs);
    }

    function bootPoll(root) {
        for (const el of candidates(root, "[heimdall-poll]"))
            reconcilePoll(el);
    }

    return {
        bootLoads,
        bootPoll,
        bootScroll,
        bootVisible,
        matchesTriggerAttr
    };
}
