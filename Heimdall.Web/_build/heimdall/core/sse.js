import {
    getAttr,
    isElement,
    resolveTarget,
    truthyAttr
} from "./utils.js";

export function createSseRuntime({
    global,
    getConfig,
    emit,
    dbg,
    dom,
    boot,
    clearBifrostSubscribeToken,
    ensureBifrostSubscribeToken,
    matchesTriggerAttr,
    defaultBifrostEndpoint
}) {
    const _sseByElement = new WeakMap();
    const _sseStates = new Set();
    const _sseConnections = new Map();

    function getSseTopic(el) {
        const t1 = getAttr(el, "heimdall-sse");
        if (t1 && t1.trim())
            return t1.trim();

        const t2 = getAttr(el, "heimdall-sse-topic");
        if (t2 && t2.trim())
            return t2.trim();

        return null;
    }

    function readSseConfig(el, options) {
        options = options || {};

        const config = getConfig();
        const topic = options.topic != null
            ? String(options.topic || "").trim()
            : getSseTopic(el);
        const eventName = (options.event != null
            ? String(options.event || "")
            : (getAttr(el, "heimdall-sse-event") || config.sseEventName || "heimdall")).trim();
        const target = options.target != null
            ? options.target
            : (getAttr(el, "heimdall-sse-target") || el);
        const swap = String(options.swap != null
            ? options.swap
            : (getAttr(el, "heimdall-sse-swap") || config.sseDefaultSwap || "none")).toLowerCase();
        const disabled = options.disable != null
            ? !!options.disable
            : truthyAttr(el, "heimdall-sse-disable", false);

        return {
            topic,
            eventName,
            target,
            swap,
            disabled,
            programmatic: !!options.programmatic
        };
    }

    function getBifrostBaseUrl() {
        const config = getConfig();
        return config.endpoints && config.endpoints.bifrost
            ? config.endpoints.bifrost
            : defaultBifrostEndpoint;
    }

    function buildBifrostUrl(topic, st) {
        const url = new URL(getBifrostBaseUrl(), global.location?.origin || undefined);
        url.searchParams.set("topic", topic);
        if (st)
            url.searchParams.set("st", st);
        return url.toString();
    }

    function getConnectionKey(topic) {
        const url = new URL(getBifrostBaseUrl(), global.location?.origin || undefined);
        return `${url.toString()}\n${topic}`;
    }

    function getStateUrl(state) {
        if (!state)
            return null;

        if (state.connection && !state.closed)
            return state.connection.url || state.url || null;

        return state.url || null;
    }

    function clearReconnectTimer(connection) {
        if (!connection || !connection.reconnectTimerId)
            return;

        try {
            global.clearTimeout(connection.reconnectTimerId);
        }
        catch { /* ignore */ }

        connection.reconnectTimerId = null;
    }

    function closeEventSource(connection) {
        if (!connection)
            return;

        const es = connection.es;
        connection.es = null;
        connection.connecting = false;
        connection.eventHandlers.clear();

        try {
            if (es)
                es.close();
        }
        catch { /* ignore */ }
    }

    function closeSseConnection(connection, reason) {
        if (!connection || connection.closed)
            return;

        connection.closed = true;
        connection.paused = false;
        connection.pauseReason = null;
        connection.connectAttempt++;
        clearReconnectTimer(connection);
        closeEventSource(connection);

        try {
            _sseConnections.delete(connection.key);
        }
        catch { /* ignore */ }

        dbg("sse connection closed", { topic: connection.topic, reason: reason || "closed" });
    }

    function closeSseState(state, reason) {
        if (!state || state.closed)
            return;

        const connection = state.connection;
        state.url = getStateUrl(state);
        state.closed = true;
        state.paused = false;
        state.pauseReason = null;

        try {
            _sseByElement.delete(state.el);
        }
        catch { /* ignore */ }

        try {
            _sseStates.delete(state);
        }
        catch { /* ignore */ }

        if (connection) {
            try {
                connection.subscribers.delete(state);
                pruneConnectionEventListeners(connection);
            }
            catch { /* ignore */ }
        }

        state.connection = null;

        emit("heimdall:sse-close", {
            topic: state.topic,
            url: state.url,
            reason: reason || "closed",
            el: state.el
        });

        dbg("sse closed", { topic: state.topic, reason: reason || "closed" });

        if (connection && !connection.closed && connection.subscribers.size === 0)
            closeSseConnection(connection, reason || "empty");
    }

    function closeSseConnectionSubscribers(connection, reason) {
        if (!connection)
            return;

        for (const state of Array.from(connection.subscribers)) {
            closeSseState(state, reason);
        }

        closeSseConnection(connection, reason);
    }

    function getReconnectDelayMs(connection) {
        const config = getConfig();
        const initial = Math.max(0, numberConfig(config.sseReconnectDelayMs, 250));
        const max = Math.max(initial, numberConfig(config.sseReconnectMaxDelayMs, 10000));
        const factor = Math.max(1, numberConfig(config.sseReconnectBackoffFactor, 2));
        const delay = initial * Math.pow(factor, Math.max(0, connection.retryCount));
        return Math.min(max, delay);
    }

    function numberConfig(value, fallback) {
        const n = Number(value);
        return Number.isFinite(n) ? n : fallback;
    }

    function isPermanentTokenFailure(error) {
        const status = error && Number(error.status);
        return status === 400 || status === 403 || status === 404;
    }

    function tokenFailureReason(error) {
        const status = error && Number(error.status);
        if (status === 400)
            return "token-rejected";
        if (status === 403)
            return "token-forbidden";
        if (status === 404)
            return "token-endpoint-not-found";

        return "token-failed";
    }

    function validateStateElement(state) {
        if (!state || state.closed)
            return false;

        if (!state.el || !state.el.isConnected) {
            closeSseState(state, "disconnected");
            return false;
        }

        if (truthyAttr(state.el, "heimdall-sse-disable", false)) {
            closeSseState(state, "disabled");
            return false;
        }

        if (!state.programmatic) {
            const currentTopic = getSseTopic(state.el);

            if (!currentTopic) {
                closeSseState(state, "topic-removed");
                return false;
            }

            if (currentTopic !== state.topic) {
                const el = state.el;
                closeSseState(state, "topic-changed");
                attachSse(el);
                return false;
            }
        }

        return true;
    }

    function validateSseConnection(connection) {
        if (!connection || connection.closed)
            return false;

        for (const state of Array.from(connection.subscribers)) {
            validateStateElement(state);
        }

        if (connection.closed)
            return false;

        if (connection.subscribers.size === 0) {
            closeSseConnection(connection, "empty");
            return false;
        }

        return true;
    }

    function isOffline() {
        return !!(global.navigator && global.navigator.onLine === false);
    }

    function getPauseReason() {
        if (isOffline())
            return "offline";

        if (getConfig().ssePauseWhenHidden && document.hidden)
            return "hidden";

        return null;
    }

    function emitForConnectionSubscribers(connection, eventName, detail) {
        if (!connection)
            return;

        for (const state of Array.from(connection.subscribers)) {
            if (state.closed)
                continue;

            emit(eventName, {
                ...detail,
                topic: connection.topic,
                url: connection.url,
                el: state.el
            });
        }
    }

    function pauseSseConnection(connection, reason, options) {
        options = options || {};

        if (!connection || connection.closed)
            return false;

        if (!validateSseConnection(connection))
            return false;

        const nextReason = reason || "paused";
        const wasPaused = !!connection.paused;
        const previousReason = connection.pauseReason || null;

        connection.paused = true;
        connection.pauseReason = nextReason;

        for (const state of Array.from(connection.subscribers)) {
            state.paused = true;
            state.pauseReason = nextReason;
        }

        if (!options.prepared) {
            connection.connectAttempt++;
            clearReconnectTimer(connection);
            closeEventSource(connection);
        } else {
            clearReconnectTimer(connection);
        }

        if (!wasPaused || previousReason !== nextReason) {
            emitForConnectionSubscribers(connection, "heimdall:sse-pause", {
                reason: nextReason,
                previousReason
            });

            dbg("sse paused", { topic: connection.topic, reason: nextReason });
        }

        return true;
    }

    function pauseAllSse(reason) {
        for (const connection of Array.from(_sseConnections.values())) {
            pauseSseConnection(connection, reason);
        }
    }

    function resumeSseConnection(connection, reason) {
        if (!connection || connection.closed || !connection.paused)
            return false;

        if (!validateSseConnection(connection))
            return false;

        const blockedReason = getPauseReason();
        if (blockedReason) {
            pauseSseConnection(connection, blockedReason);
            return false;
        }

        const previousReason = connection.pauseReason || null;
        connection.paused = false;
        connection.pauseReason = null;

        for (const state of Array.from(connection.subscribers)) {
            state.paused = false;
            state.pauseReason = null;
        }

        emitForConnectionSubscribers(connection, "heimdall:sse-resume", {
            reason: reason || "resume",
            previousReason
        });

        dbg("sse resumed", { topic: connection.topic, reason: reason || "resume", previousReason });
        connectSseConnection(connection);
        return true;
    }

    function resumePausedSse(reason) {
        for (const connection of Array.from(_sseConnections.values())) {
            resumeSseConnection(connection, reason);
        }
    }

    function scheduleReconnect(connection, reason, error) {
        if (!connection || connection.closed)
            return;

        if (connection.paused)
            return;

        if (!validateSseConnection(connection))
            return;

        if (connection.reconnectTimerId)
            return;

        connection.connectAttempt++;
        closeEventSource(connection);

        if (typeof clearBifrostSubscribeToken === "function")
            clearBifrostSubscribeToken(connection.topic);

        const pauseReason = getPauseReason();
        if (pauseReason) {
            pauseSseConnection(connection, pauseReason, { prepared: true });
            return;
        }

        const delayMs = getReconnectDelayMs(connection);
        connection.retryCount++;

        emitForConnectionSubscribers(connection, "heimdall:sse-reconnect-scheduled", {
            reason: reason || "reconnect",
            attempt: connection.retryCount,
            delayMs,
            status: error && error.status ? error.status : null
        });

        dbg("sse reconnect scheduled", {
            topic: connection.topic,
            reason: reason || "reconnect",
            attempt: connection.retryCount,
            delayMs
        });

        connection.reconnectTimerId = global.setTimeout(() => {
            connection.reconnectTimerId = null;
            connectSseConnection(connection);
        }, delayMs);
    }

    async function connectSseConnection(connection) {
        if (!validateSseConnection(connection))
            return;

        const pauseReason = getPauseReason();
        if (pauseReason) {
            pauseSseConnection(connection, pauseReason);
            return;
        }

        clearReconnectTimer(connection);

        if (connection.es || connection.connecting)
            return;

        const attemptId = ++connection.connectAttempt;
        connection.connecting = true;

        try {
            const st = await ensureBifrostSubscribeToken(connection.topic);

            if (connection.closed || attemptId !== connection.connectAttempt)
                return;

            if (!validateSseConnection(connection))
                return;

            const url = buildBifrostUrl(connection.topic, st);
            connection.url = url;

            for (const state of Array.from(connection.subscribers)) {
                state.url = url;
            }

            let es;
            try {
                es = new global.EventSource(url);
            } catch (e) {
                connection.connecting = false;
                emitForConnectionSubscribers(connection, "heimdall:sse-error", { error: e });
                if (getConfig().debug) {
                    // eslint-disable-next-line no-console
                    console.error(`[Heimdall] SSE connect failed`, e);
                }
                scheduleReconnect(connection, "connect-failed", e);
                return;
            }

            if (connection.closed || attemptId !== connection.connectAttempt) {
                try {
                    es.close();
                }
                catch { /* ignore */ }
                return;
            }

            connection.es = es;
            connection.connecting = false;
            connection.eventHandlers.clear();

            es.onopen = () => {
                if (connection.closed)
                    return;

                connection.retryCount = 0;
                connection.openedAt = Date.now();
                connection.lastMessageAt = Date.now();
                emitForConnectionSubscribers(connection, "heimdall:sse-open", {});
                dbg("sse open", { topic: connection.topic, url });
            };

            es.onmessage = (ev) => {
                dispatchSsePayload(connection, "message", ev, ev && ev.data != null ? ev.data : "");
            };

            syncConnectionEventListeners(connection);

            es.onerror = (e) => {
                if (connection.closed)
                    return;

                emitForConnectionSubscribers(connection, "heimdall:sse-error", { error: e });
                if (getConfig().debug) {
                    // eslint-disable-next-line no-console
                    console.warn(`[Heimdall] SSE error; reconnecting with a fresh token`, { topic: connection.topic, url: connection.url }, e);
                }

                scheduleReconnect(connection, "eventsource-error", e);
            };
        } catch (e) {
            if (connection.closed || attemptId !== connection.connectAttempt)
                return;

            connection.connecting = false;
            emitForConnectionSubscribers(connection, "heimdall:sse-error", { error: e });
            if (getConfig().debug) {
                // eslint-disable-next-line no-console
                console.error(`[Heimdall] SSE token/connect failed`, e);
            }

            if (isPermanentTokenFailure(e)) {
                closeSseConnectionSubscribers(connection, tokenFailureReason(e));
                return;
            }

            scheduleReconnect(connection, "token-failed", e);
        }
    }

    function ensureConnectionEventListener(connection, eventName) {
        if (!connection || !connection.es || !eventName || eventName === "message")
            return;

        if (connection.eventHandlers.has(eventName))
            return;

        const handler = (ev) => {
            dispatchSsePayload(connection, eventName, ev, ev && ev.data != null ? ev.data : "");
        };

        connection.eventHandlers.set(eventName, handler);
        connection.es.addEventListener(eventName, handler);
    }

    function syncConnectionEventListeners(connection) {
        if (!connection || !connection.es)
            return;

        for (const state of Array.from(connection.subscribers)) {
            if (!state.closed)
                ensureConnectionEventListener(connection, state.eventName);
        }

        pruneConnectionEventListeners(connection);
    }

    function pruneConnectionEventListeners(connection) {
        if (!connection || !connection.es || !connection.eventHandlers)
            return;

        for (const [eventName, handler] of Array.from(connection.eventHandlers.entries())) {
            const stillUsed = Array.from(connection.subscribers).some(state => !state.closed && state.eventName === eventName);
            if (stillUsed)
                continue;

            try {
                if (typeof connection.es.removeEventListener === "function")
                    connection.es.removeEventListener(eventName, handler);
            }
            catch { /* ignore */ }

            connection.eventHandlers.delete(eventName);
        }
    }

    function dispatchSsePayload(connection, eventName, ev, rawData) {
        if (!connection || connection.closed || connection.paused)
            return;

        if (!validateSseConnection(connection))
            return;

        connection.lastMessageAt = Date.now();

        for (const state of Array.from(connection.subscribers)) {
            if (state.closed || state.paused || state.eventName !== eventName)
                continue;

            handleSsePayload(state, ev, rawData);
        }
    }

    function handleSsePayload(state, ev, rawData) {
        if (state.closed || state.paused || (state.connection && state.connection.paused))
            return;
        if (!state.el || !state.el.isConnected) {
            closeSseState(state, "disconnected");
            return;
        }

        const data = rawData != null ? String(rawData) : "";
        const targetEl = resolveTarget(state.target, state.el);
        const swapMode = state.swap || "none";
        const url = getStateUrl(state);

        let html = data;
        let abortSwap = false;
        let abortReason = null;
        let redirectUrl = null;

        try {
            const oob = dom.processOob(html, state.el);
            html = oob.html;
            abortSwap = !!oob.abortSwap;
            abortReason = oob.abortReason || null;
            redirectUrl = oob.redirectUrl || null;
        } catch (e) {
            emit("heimdall:sse-error", { topic: state.topic, url, el: state.el, error: e });
            if (getConfig().debug) {
                // eslint-disable-next-line no-console
                console.error(`[Heimdall] SSE OOB processing error`, e);
            }
            return;
        }

        if (redirectUrl) {
            emit("heimdall:sse-redirect", {
                topic: state.topic,
                url,
                el: state.el,
                redirectUrl
            });

            dbg("sse redirecting", { topic: state.topic, redirectUrl });
            global.location.href = redirectUrl;
            return;
        }

        if (abortSwap) {
            emit("heimdall:sse-abort", { topic: state.topic, url, el: state.el, target: targetEl, swap: swapMode, reason: abortReason });
            dbg("sse swap aborted", { topic: state.topic, reason: abortReason, target: targetEl });
        }

        if (!abortSwap && swapMode !== "none" && targetEl) {
            const mainTpl = dom.parseHtmlToTemplate(html);
            dom.stripInvocationsFromFragment(mainTpl.content);
            dom.stripAbortsFromFragment(mainTpl.content);
            dom.stripRedirectsFromFragment(mainTpl.content);

            const { didApply, appliedRoot } = dom.applySwap(targetEl, mainTpl.content, swapMode);

            if (didApply && !getConfig().observeDom) {
                try {
                    boot(appliedRoot || targetEl);
                }
                catch { /* ignore */ }
            }
        }

        emit("heimdall:sse-message", {
            topic: state.topic,
            event: state.eventName,
            url,
            id: ev && ev.lastEventId ? String(ev.lastEventId) : null,
            bytes: data ? data.length : 0,
            el: state.el
        });
    }

    function getOrCreateSseConnection(topic) {
        const key = getConnectionKey(topic);
        let connection = _sseConnections.get(key);

        if (connection && !connection.closed)
            return connection;

        connection = {
            key,
            topic,
            url: null,
            es: null,
            closed: false,
            openedAt: Date.now(),
            lastMessageAt: 0,
            connecting: false,
            reconnectTimerId: null,
            retryCount: 0,
            connectAttempt: 0,
            paused: false,
            pauseReason: null,
            subscribers: new Set(),
            eventHandlers: new Map()
        };

        _sseConnections.set(key, connection);
        return connection;
    }

    function attachSse(el, options) {
        if (!el || !isElement(el))
            return null;

        const next = readSseConfig(el, options);
        const existing = _sseByElement.get(el);

        if (next.disabled) {
            if (existing)
                closeSseState(existing, "disabled");
            return null;
        }

        if (!next.topic) {
            if (existing && !existing.programmatic)
                closeSseState(existing, "topic-removed");
            return existing || null;
        }

        if (existing && !existing.closed) {
            if (existing.topic === next.topic) {
                const connection = existing.connection;
                existing.eventName = next.eventName;
                existing.target = next.target;
                existing.swap = next.swap;
                existing.programmatic = existing.programmatic || next.programmatic;
                existing.paused = !!(connection && connection.paused);
                existing.pauseReason = connection ? connection.pauseReason : null;

                if (connection) {
                    ensureConnectionEventListener(connection, existing.eventName);
                    pruneConnectionEventListeners(connection);
                    if (connection.paused)
                        resumeSseConnection(connection, "config-updated");
                    else
                        connectSseConnection(connection);
                }

                return existing;
            }

            closeSseState(existing, "topic-changed");
        }

        if (!("EventSource" in global)) {
            if (getConfig().debug) {
                // eslint-disable-next-line no-console
                console.warn(`[Heimdall] EventSource not available; SSE disabled.`, el);
            }
            return null;
        }

        const connection = getOrCreateSseConnection(next.topic);
        const state = {
            el,
            topic: next.topic,
            url: connection.url,
            eventName: next.eventName,
            target: next.target,
            swap: next.swap,
            closed: false,
            paused: !!connection.paused,
            pauseReason: connection.pauseReason || null,
            programmatic: next.programmatic,
            connection
        };

        _sseByElement.set(el, state);
        _sseStates.add(state);
        connection.subscribers.add(state);

        if (connection.es)
            ensureConnectionEventListener(connection, state.eventName);

        if (connection.paused)
            resumeSseConnection(connection, "subscriber-added");
        else
            connectSseConnection(connection);

        return state;
    }

    function bootSse(root) {
        const scope = isElement(root) ? root : document;

        if (isElement(root) && (
            _sseByElement.get(root) ||
            matchesTriggerAttr(root, "heimdall-sse") ||
            matchesTriggerAttr(root, "heimdall-sse-topic")
        )) {
            attachSse(root);
        }

        for (const el of scope.querySelectorAll("[heimdall-sse],[heimdall-sse-topic]"))
            attachSse(el);
    }

    let _sseSweepInstalled = false;
    let _sseGlobalEventsInstalled = false;

    function installSseGlobalEvents() {
        if (_sseGlobalEventsInstalled)
            return;
        _sseGlobalEventsInstalled = true;

        if (global && typeof global.addEventListener === "function") {
            global.addEventListener("offline", () => {
                pauseAllSse("offline");
            });

            global.addEventListener("online", () => {
                resumePausedSse("online");
            });
        }

        document.addEventListener("visibilitychange", () => {
            if (!getConfig().ssePauseWhenHidden)
                return;

            if (document.hidden) {
                pauseAllSse("hidden");
                return;
            }

            resumePausedSse("visible");

            try {
                bootSse(document);
            }
            catch { /* ignore */ }
        });
    }

    function installSseSweeper() {
        if (_sseSweepInstalled)
            return;
        _sseSweepInstalled = true;
        installSseGlobalEvents();

        const sweepIntervalMs = getConfig().sseSweepIntervalMs || 5000;

        setInterval(() => {
            const pauseReason = getPauseReason();
            if (pauseReason) {
                pauseAllSse(pauseReason);
                return;
            }

            for (const state of Array.from(_sseStates)) {
                validateStateElement(state);
            }

            for (const connection of Array.from(_sseConnections.values())) {
                if (!connection || connection.closed)
                    continue;

                if (!validateSseConnection(connection))
                    continue;

                if (connection.paused) {
                    resumeSseConnection(connection, "sweep");
                    continue;
                }

                connectSseConnection(connection);
            }
        }, sweepIntervalMs);
    }

    // Programmatic SSE API
    function sseConnect(topic, options) {
        options = options || {};
        const el = options.element || document.body;

        if (!isElement(el))
            throw new Error("Heimdall.sse.connect requires an element (options.element).");

        const state = attachSse(el, {
            topic,
            target: options.target,
            swap: options.swap,
            event: options.event,
            disable: options.disable,
            programmatic: true
        });

        return {
            close: () => closeSseState(state, "manual"),
            get topic() { return state ? state.topic : null; },
            get url() { return state ? getStateUrl(state) : null; }
        };
    }

    function sseDisconnect(element) {
        const el = resolveTarget(element, null);
        if (!el)
            return;

        const state = _sseByElement.get(el);
        if (state) closeSseState(state, "manual");
    }

    function sseDisconnectAll() {
        for (const state of Array.from(_sseStates)) {
            closeSseState(state, "manual-all");
        }
    }

    return {
        bootSse,
        installSseSweeper,
        sseConnect,
        sseDisconnect,
        sseDisconnectAll
    };
}
