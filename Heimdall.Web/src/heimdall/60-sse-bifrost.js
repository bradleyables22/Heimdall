    const _sseByElement = new WeakMap();
    const _sseStates = new Set();

    function getSseTopic(el) {
        const t1 = getAttr(el, "heimdall-sse");
        if (t1 && t1.trim())
            return t1.trim();

        const t2 = getAttr(el, "heimdall-sse-topic");
        if (t2 && t2.trim())
            return t2.trim();

        return null;
    }

    function buildBifrostUrl(topic, st) {
        const base = Heimdall.config.endpoints && Heimdall.config.endpoints.bifrost
            ? Heimdall.config.endpoints.bifrost
            : DEFAULT_BIFROST_ENDPOINT;

        const url = new URL(base, global.location?.origin || undefined);
        url.searchParams.set("topic", topic);
        if (st)
            url.searchParams.set("st", st);
        return url.toString();
    }

    function closeSseState(state, reason) {
        if (!state)
            return;
        state.closed = true;

        try {
            if (state.es)
                state.es.close();
        }
        catch { /* ignore */ }

        try {
            _sseByElement.delete(state.el);
        }
        catch { /* ignore */ }

        try {
            _sseStates.delete(state);
        }
        catch { /* ignore */ }

        emit("heimdall:sse-close", {
            topic: state.topic,
            url: state.url,
            reason: reason || "closed",
            el: state.el
        });

        dbg("sse closed", { topic: state.topic, reason: reason || "closed" });
    }

    function handleSsePayload(state, ev, rawData) {
        if (state.closed)
            return;
        if (!state.el || !state.el.isConnected) {
            closeSseState(state, "disconnected");
            return;
        }

        state.lastMessageAt = Date.now();

        const data = rawData != null ? String(rawData) : "";
        const targetEl = resolveTarget(state.target, state.el);
        const swapMode = state.swap || "none";

        let html = data;
        let abortSwap = false;
        let abortReason = null;
        let redirectUrl = null;

        try {
            const oob = processOob(html, state.el);
            html = oob.html;
            abortSwap = !!oob.abortSwap;
            abortReason = oob.abortReason || null;
            redirectUrl = oob.redirectUrl || null;
        } catch (e) {
            emit("heimdall:sse-error", { topic: state.topic, url: state.url, el: state.el, error: e });
            if (Heimdall.config.debug) {
                // eslint-disable-next-line no-console
                console.error(`[Heimdall] SSE OOB processing error`, e);
            }
            return;
        }

        if (redirectUrl) {
            emit("heimdall:sse-redirect", {
                topic: state.topic,
                url: state.url,
                el: state.el,
                redirectUrl
            });

            dbg("sse redirecting", { topic: state.topic, redirectUrl });
            global.location.href = redirectUrl;
            return;
        }

        if (abortSwap) {
            emit("heimdall:sse-abort", { topic: state.topic, url: state.url, el: state.el, target: targetEl, swap: swapMode, reason: abortReason });
            dbg("sse swap aborted", { topic: state.topic, reason: abortReason, target: targetEl });
        }

        if (!abortSwap && swapMode !== "none" && targetEl) {
            const mainTpl = parseHtmlToTemplate(html);
            stripInvocationsFromFragment(mainTpl.content);
            stripAbortsFromFragment(mainTpl.content);
            stripRedirectsFromFragment(mainTpl.content);

            const { didApply, appliedRoot } = applySwap(targetEl, mainTpl.content, swapMode);

            if (didApply && !Heimdall.config.observeDom) {
                try {
                    boot(appliedRoot || targetEl);
                }
                catch { /* ignore */ }
            }
        }

        emit("heimdall:sse-message", {
            topic: state.topic,
            url: state.url,
            id: ev && ev.lastEventId ? String(ev.lastEventId) : null,
            bytes: data ? data.length : 0,
            el: state.el
        });
    }

    function attachSse(el) {
        if (!el || !isElement(el))
            return;

        const disable = truthyAttr(el, "heimdall-sse-disable", false);
        if (disable) {
            const prev = _sseByElement.get(el);
            if (prev) closeSseState(prev, "disabled");
            return;
        }

        const topic = getSseTopic(el);
        if (!topic)
            return;

        const existing = _sseByElement.get(el);
        if (existing) {
            if (existing.topic === topic && !existing.closed)
                return;
            closeSseState(existing, "topic-changed");
        }

        if (!("EventSource" in global)) {
            if (Heimdall.config.debug) {
                // eslint-disable-next-line no-console
                console.warn(`[Heimdall] EventSource not available; SSE disabled.`, el);
            }
            return;
        }

        // Snapshot all attrs synchronously before any async work.
        // The programmatic sseConnect() API restores attrs in a finally block
        // immediately after calling attachSse() � snapshotting here ensures
        // the async continuation below uses the values that were present at
        // call time, not whatever the DOM looks like later.
        const eventName = (getAttr(el, "heimdall-sse-event") || Heimdall.config.sseEventName || "heimdall").trim();
        const target = getAttr(el, "heimdall-sse-target") || el;
        const swap = (getAttr(el, "heimdall-sse-swap") || Heimdall.config.sseDefaultSwap || "none").toLowerCase();

        const state = {
            el,
            topic,
            url: null,
            eventName,
            target,
            swap,
            es: null,
            closed: false,
            openedAt: Date.now(),
            lastMessageAt: 0,
            connecting: true
        };

        _sseByElement.set(el, state);
        _sseStates.add(state);

        (async () => {
            try {
                const st = await ensureBifrostSubscribeToken(topic);

                if (state.closed)
                    return;
                if (!state.el || !state.el.isConnected) {
                    closeSseState(state, "disconnected");
                    return;
                }

                const url = buildBifrostUrl(topic, st);
                state.url = url;

                let es;
                try {
                    es = new EventSource(url);
                } catch (e) {
                    emit("heimdall:sse-error", { topic, url, el, error: e });
                    if (Heimdall.config.debug) {
                        // eslint-disable-next-line no-console
                        console.error(`[Heimdall] SSE connect failed`, e);
                    }
                    closeSseState(state, "connect-failed");
                    return;
                }

                state.es = es;
                state.connecting = false;

                es.onopen = () => {
                    state.lastMessageAt = Date.now();
                    emit("heimdall:sse-open", { topic, url, el });
                    dbg("sse open", { topic, url });
                };

                if (eventName && eventName !== "message") {
                    es.addEventListener(eventName, (ev) => {
                        handleSsePayload(state, ev, ev && ev.data != null ? ev.data : "");
                    });
                }

                es.onmessage = (ev) => {
                    if (eventName !== "message")
                        return;
                    handleSsePayload(state, ev, ev && ev.data != null ? ev.data : "");
                };

                es.onerror = (e) => {
                    emit("heimdall:sse-error", { topic, url, el, error: e });
                    if (Heimdall.config.debug) {
                        // eslint-disable-next-line no-console
                        console.warn(`[Heimdall] SSE error (auto-reconnect expected)`, { topic, url }, e);
                    }
                };
            } catch (e) {
                emit("heimdall:sse-error", { topic, url: state.url, el, error: e });
                if (Heimdall.config.debug) {
                    // eslint-disable-next-line no-console
                    console.error(`[Heimdall] SSE token/connect failed`, e);
                }
                closeSseState(state, "token-failed");
            }
        })();
    }

    function bootSse(root) {
        const scope = isElement(root) ? root : document;

        if (isElement(root) && (matchesTriggerAttr(root, "heimdall-sse") || matchesTriggerAttr(root, "heimdall-sse-topic")))
            attachSse(root);

        for (const el of scope.querySelectorAll("[heimdall-sse],[heimdall-sse-topic]"))
            attachSse(el);
    }

    let _sseSweepInstalled = false;

    function installSseSweeper() {
        if (_sseSweepInstalled)
            return;
        _sseSweepInstalled = true;

        const sweepIntervalMs = Heimdall.config.sseSweepIntervalMs || 5000;

        setInterval(() => {
            for (const state of Array.from(_sseStates)) {
                if (!state || state.closed)
                    continue;

                if (!state.el || !state.el.isConnected) {
                    closeSseState(state, "disconnected");
                    continue;
                }

                if (Heimdall.config.ssePauseWhenHidden && document.hidden) {
                    closeSseState(state, "hidden");
                    continue;
                }

                if (truthyAttr(state.el, "heimdall-sse-disable", false)) {
                    closeSseState(state, "disabled");
                    continue;
                }

                const currentTopic = getSseTopic(state.el);
                if (currentTopic && currentTopic !== state.topic) {
                    closeSseState(state, "topic-changed");
                    continue;
                }
            }

            if (!document.hidden && Heimdall.config.ssePauseWhenHidden) {
                try {
                    bootSse(document);
                }
                catch { /* ignore */ }
            }
        }, sweepIntervalMs);

        if (Heimdall.config.ssePauseWhenHidden) {
            document.addEventListener("visibilitychange", () => {
                if (!document.hidden) {
                    try {
                        bootSse(document);
                    }
                    catch { /* ignore */ }
                }
            });
        }
    }

    // Programmatic SSE API
    function sseConnect(topic, options) {
        options = options || {};
        const el = options.element || document.body;

        if (!isElement(el))
            throw new Error("Heimdall.sse.connect requires an element (options.element).");

        // Snapshot previous attrs so we can restore them after attachSse() reads them.
        // attachSse() captures all SSE config values synchronously before returning,
        // so the restore in finally is safe � the async token fetch uses the snapshot.
        const prev = {
            sse: el.getAttribute("heimdall-sse"),
            sseTopic: el.getAttribute("heimdall-sse-topic"),
            tgt: el.getAttribute("heimdall-sse-target"),
            swp: el.getAttribute("heimdall-sse-swap"),
            evt: el.getAttribute("heimdall-sse-event"),
            dis: el.getAttribute("heimdall-sse-disable")
        };

        try {
            el.setAttribute("heimdall-sse", String(topic || "").trim());
            if (options.target)
                el.setAttribute("heimdall-sse-target", options.target);
            if (options.swap)
                el.setAttribute("heimdall-sse-swap", options.swap);
            if (options.event)
                el.setAttribute("heimdall-sse-event", options.event);
            if (options.disable != null)
                el.setAttribute("heimdall-sse-disable", options.disable ? "true" : "false");

            attachSse(el);

            const state = _sseByElement.get(el);
            return {
                close: () => closeSseState(state, "manual"),
                get topic() { return state ? state.topic : null; },
                get url() { return state ? state.url : null; }
            };
        } finally {
            if (prev.sse == null)
                el.removeAttribute("heimdall-sse");
            else
                el.setAttribute("heimdall-sse", prev.sse);
            if (prev.sseTopic == null)
                el.removeAttribute("heimdall-sse-topic");
            else
                el.setAttribute("heimdall-sse-topic", prev.sseTopic);
            if (prev.tgt == null)
                el.removeAttribute("heimdall-sse-target");
            else
                el.setAttribute("heimdall-sse-target", prev.tgt);
            if (prev.swp == null)
                el.removeAttribute("heimdall-sse-swap");
            else
                el.setAttribute("heimdall-sse-swap", prev.swp);
            if (prev.evt == null)
                el.removeAttribute("heimdall-sse-event");
            else
                el.setAttribute("heimdall-sse-event", prev.evt);
            if (prev.dis == null)
                el.removeAttribute("heimdall-sse-disable");
            else
                el.setAttribute("heimdall-sse-disable", prev.dis);
        }
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

