(function (global) {
    "use strict";

    // ============================================================
    // Heimdall.js (v1)
    // - Content actions: POST /__heimdall/v1/content/actions
    // - CSRF token:      GET  /__heimdall/v1/csrf
    // - Attributes:
    //   Triggers:
    //   * heimdall-content-load="Action.Id"
    //   * heimdall-content-click="Action.Id"
    //   * heimdall-content-change="Action.Id"
    //   * heimdall-content-input="Action.Id"
    //   * heimdall-content-submit="Action.Id"
    //   * heimdall-content-keydown="Action.Id"
    //   * heimdall-content-blur="Action.Id"         (implemented via focusout delegation)
    //   * heimdall-content-hover="Action.Id"        (implemented via mouseover delegation + delay)
    //   * heimdall-content-visible="Action.Id"      (IntersectionObserver)
    //   * heimdall-content-scroll="Action.Id"       (non-bubbling; per-element listener)
    //
    //   Common:
    //   * heimdall-content-target="#selector" (or element)
    //   * heimdall-content-swap="inner|outer|beforeend|afterbegin"
    //   * heimdall-content-disable="true|false"      (default varies by trigger)
    //   * heimdall-prevent-default="true|false"      (anchors; submit; some keydown)
    //
    //   Payload:
    //   * heimdall-payload='{"json":1}'
    //   * heimdall-payload-from="closest-form|self|#formSelector|ref:Path.To.Obj"
    //   * heimdall-payload-ref="Path.To.Obj"
    //
    //   Trigger options:
    //   * heimdall-debounce="ms"                     (input/change)
    //   * heimdall-key="Enter|Escape|...|13"         (keydown)
    //   * heimdall-hover-delay="ms"                  (hover)
    //   * heimdall-visible-once="true|false"         (visible)
    //   * heimdall-scroll-threshold="px"             (scroll)
    //   * heimdall-poll="ms"                         (load polling)
    // ============================================================

    // ============================================================
    // OOB (Out-of-band swaps)
    //
    // Convention:
    //   Any node with heimdall-oob="true" is an instruction:
    //     heimdall-content-target="#selector"
    //     heimdall-content-swap="inner|outer|beforeend|afterbegin"
    //   Payload HTML is the node's innerHTML (use <template> recommended).
    //
    // Example:
    //   <template heimdall-oob="true" heimdall-content-target="#modalHost" heimdall-content-swap="inner"></template>
    //   <template heimdall-oob="true" heimdall-content-target="#row-123" heimdall-content-swap="outer"></template>
    // ============================================================

    const VERSION = "1.1.0";
    const API_VERSION = 1;

    const DEFAULT_BASE_PATH = "/__heimdall";
    const DEFAULT_CONTENT_ENDPOINT = `${DEFAULT_BASE_PATH}/v${API_VERSION}/content/actions`;
    const DEFAULT_CSRF_ENDPOINT = `${DEFAULT_BASE_PATH}/v${API_VERSION}/csrf`;

    const ACTION_HEADER = "X-Heimdall-Content-Action";
    const CSRF_HEADER = "RequestVerificationToken";


    function isElement(x) {
        return x && x.nodeType === 1;
    }

    function resolveTarget(target, fallbackEl) {
        if (!target)
            return fallbackEl || null;

        if (isElement(target))
            return target;

        if (typeof target === "string")
            return document.querySelector(target);

        return fallbackEl || null;
    }

    function safeJsonParse(text) {
        try {
            return JSON.parse(text);
        } catch {
            return null;
        }
    }

    async function safeText(res) {
        try {
            return await res.text();
        } catch {
            return "";
        }
    }

    function setHtml(targetEl, html, swap) {
        const mode = (swap || "inner").toLowerCase();

        switch (mode) {
            case "outer":
                targetEl.outerHTML = html;
                break;
            case "beforeend":
                targetEl.insertAdjacentHTML("beforeend", html);
                break;
            case "afterbegin":
                targetEl.insertAdjacentHTML("afterbegin", html);
                break;
            default:
                targetEl.innerHTML = html;
                break;
        }
    }

    function onReady(fn) {
        if (document.readyState === "loading") {
            document.addEventListener("DOMContentLoaded", fn, { once: true });
        } else {
            fn();
        }
    }

    function getAttr(el, name) {
        const v = el.getAttribute(name);
        return v == null ? null : v;
    }

    function truthyAttr(el, name, defaultValue) {
        const v = getAttr(el, name);
        if (v == null)
            return !!defaultValue;

        const s = String(v).toLowerCase().trim();
        if (s === "" || s === "true" || s === "1" || s === "yes")
            return true;
        if (s === "false" || s === "0" || s === "no")
            return false;

        return !!defaultValue;
    }

    function intAttr(el, name, defaultValue) {
        const v = getAttr(el, name);
        if (v == null || v === "")
            return defaultValue;

        const n = parseInt(v, 10);
        return Number.isFinite(n) ? n : defaultValue;
    }

    function formDataToObject(fd) {
        const obj = {};
        for (const [k, v] of fd.entries()) {
            if (Object.prototype.hasOwnProperty.call(obj, k)) {
                if (!Array.isArray(obj[k])) obj[k] = [obj[k]];
                obj[k].push(v);
            } else {
                obj[k] = v;
            }
        }
        return obj;
    }

    function getByPath(root, path) {
        if (!path)
            return undefined;

        let cur = root;
        const parts = String(path).split(".").map(p => p.trim()).filter(Boolean);

        for (const p of parts) {
            if (cur == null)
                return undefined;
            cur = cur[p];
        }

        return cur;
    }

    function resolvePayloadRef(el) {
        // explicit attribute wins
        const ref = getAttr(el, "heimdall-payload-ref");
        if (ref)
            return getByPath(global, ref);

        // allow heimdall-payload-from="ref:Some.Path"
        const from = (getAttr(el, "heimdall-payload-from") || "").trim();
        if (from.toLowerCase().startsWith("ref:")) {
            const path = from.substring(4).trim();
            return getByPath(global, path);
        }

        return undefined;
    }

    function payloadFromElement(el) {
        // Inline JSON
        const payloadAttr = getAttr(el, "heimdall-payload");
        if (payloadAttr)
            return safeJsonParse(payloadAttr);

        // JS object reference by path (safe, no eval)
        const refObj = resolvePayloadRef(el);
        if (refObj !== undefined)
            return refObj;

        // Other sources
        const from = (getAttr(el, "heimdall-payload-from") || "").toLowerCase().trim();
        if (!from)
            return null;

        if (from === "closest-form") {
            const form = el.closest("form");
            if (!form)
                return null;
            return formDataToObject(new FormData(form));
        }

        if (from === "self") {
            const obj = {};
            for (const key in el.dataset)
                obj[key] = el.dataset[key];
            return obj;
        }

        // "#formSelector" (or any selector pointing at a FORM)
        const form = document.querySelector(from);
        if (form && form.tagName === "FORM") {
            return formDataToObject(new FormData(form));
        }

        return null;
    }

    function emit(name, detail) {
        try {
            document.dispatchEvent(new CustomEvent(name, { detail }));
        } catch {
            // ignore
        }
    }

    function dbg(...args) {
        if (Heimdall.config.debug) {
            // eslint-disable-next-line no-console
            console.debug(`[Heimdall ${VERSION}]`, ...args);
        }
    }

    function isTextLikeInput(el) {
        if (!el || el.nodeType !== 1)
            return false;
        const tag = el.tagName;
        if (tag === "TEXTAREA")
            return true;
        if (tag !== "INPUT")
            return false;

        const type = (el.getAttribute("type") || "text").toLowerCase();
        // text-ish types where input events are common
        return (
            type === "text" ||
            type === "search" ||
            type === "email" ||
            type === "url" ||
            type === "tel" ||
            type === "password" ||
            type === "number"
        );
    }

    function matchesKeySpec(e, spec) {
        if (!spec)
            return true;

        const s = String(spec).trim();
        if (!s)
            return true;

        // Numeric keyCode
        if (/^\d+$/.test(s)) {
            const code = parseInt(s, 10);
            return (e.keyCode === code) || (e.which === code);
        }

        // Named key
        return String(e.key || "").toLowerCase() === s.toLowerCase();
    }

    // CSRF token caching
    let csrfToken = null;
    let csrfTokenPromise = null;

    async function ensureCsrfToken() {
        if (csrfToken)
            return csrfToken;
        if (csrfTokenPromise)
            return csrfTokenPromise;

        csrfTokenPromise = (async () => {
            try {
                const res = await fetch(Heimdall.config.endpoints.csrf, {
                    method: "GET",
                    credentials: Heimdall.config.credentials || "same-origin",
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });

                if (!res.ok) throw new Error(`CSRF token fetch failed: ${res.status}`);

                const data = await res.json();
                csrfToken = data && data.requestToken;
                if (!csrfToken)
                    throw new Error("CSRF response missing requestToken.");
                return csrfToken;
            } finally {
                csrfTokenPromise = null;
            }
        })();

        return csrfTokenPromise;
    }

    function clearCsrfToken() {
        csrfToken = null;
        csrfTokenPromise = null;
    }
    function matchesAllowedTarget(selector, sourceEl) {
        const rule = Heimdall.config.oobAllowedTargets;

        if (!rule) return true; // allow all by default

        if (Array.isArray(rule)) {
            return rule.includes(selector);
        }

        if (typeof rule === "function") {
            try {
                return !!rule(selector, sourceEl);
            } catch {
                return false;
            }
        }

        return true;
    }

    // Processes OOB nodes and returns { html: remainingHtml, applied: count }
    function processOob(html, sourceEl) {
        if (!Heimdall.config.oobEnabled)
            return { html, applied: 0 };

        // Fast-path: avoid parsing most responses
        if (!html || html.indexOf("heimdall-oob") === -1)
            return { html, applied: 0 };

        const container = document.createElement("div");
        container.innerHTML = html;

        const oobs = container.querySelectorAll("[heimdall-oob='true']");
        if (!oobs || oobs.length === 0)
            return { html, applied: 0 };

        let applied = 0;

        for (const oobEl of oobs) {
            const targetSel = getAttr(oobEl, "heimdall-content-target");
            if (!targetSel) {
                dbg("OOB missing heimdall-content-target; skipping", oobEl);
                oobEl.remove();
                continue;
            }

            if (!matchesAllowedTarget(targetSel, sourceEl)) {
                if (Heimdall.config.debug) {
                    // eslint-disable-next-line no-console
                    console.warn(`[Heimdall ${VERSION}] OOB target not allowed: '${targetSel}'.`);
                }
                oobEl.remove();
                continue;
            }

            const swap = (getAttr(oobEl, "heimdall-content-swap") || "inner").toLowerCase();
            const targetEl = resolveTarget(targetSel, null);

            if (!targetEl) {
                dbg("OOB target not found; skipping", targetSel);
                oobEl.remove();
                continue;
            }

            const payloadHtml = oobEl.innerHTML || "";
            setHtml(targetEl, payloadHtml, swap);
            applied++;

            // If observeDom is off, newly injected triggers won't be wired unless we boot manually.
            if (!Heimdall.config.observeDom) {
                try {
                    boot(targetEl);
                } catch { /* ignore */ }
            }

            // Remove instruction so it won't be part of the normal swap output.
            oobEl.remove();
        }

        return { html: container.innerHTML, applied };
    }

    async function invoke(actionId, payload, options) {
        return _invokeWithRetry(actionId, payload, options, true);
    }

    async function _invokeWithRetry(actionId, payload, options, shouldRetry) {
        options = options || {};

        const endpointBase = options.endpoint || Heimdall.config.endpoints.contentActions;
        const targetEl = resolveTarget(options.target, options.fallbackTarget || null);
        const swap = options.swap || "inner";
        const credentials = options.credentials || Heimdall.config.credentials || "same-origin";

        const url = new URL(endpointBase, global.location?.origin || undefined);
        url.searchParams.set("action", actionId);

        const token = await ensureCsrfToken();

        const headers = {
            "Content-Type": "application/json",
            [ACTION_HEADER]: actionId,
            [CSRF_HEADER]: token
        };

        if (options.headers) {
            for (const k in options.headers)
                headers[k] = options.headers[k];
        }

        let body = "{}";

        try {
            body = payload == null ? "{}" : JSON.stringify(payload);
        } catch (e) {
            const err = new Error(`Heimdall payload is not JSON-serializable for action '${actionId}'.`);
            err.cause = e;
            emit("heimdall:error", { actionId, payload, target: targetEl, swap, status: 0, error: err });
            throw err; // dev error
        }

        const started = performance.now();
        emit("heimdall:before", { actionId, payload, target: targetEl, swap, endpoint: url.toString() });

        dbg("invoke ->", actionId, { endpoint: url.toString(), swap, target: targetEl });

        let res;

        try {
            res = await fetch(url.toString(), {
                method: "POST",
                headers,
                body,
                credentials
            });
        }
        catch (networkErr) {
            const result = {
                ok: false,
                status: 0,
                error: networkErr.message,
                response: null,
                html: null,
                ms: performance.now() - started
            };

            emit("heimdall:error", { actionId, payload, target: targetEl, swap, error: networkErr });

            return result;
        }

        // CSRF retry
        if (res.status === 400 && shouldRetry) {
            const text = await safeText(res);
            const lower = (text || "").toLowerCase();

            if (lower.includes("csrf") || lower.includes("antiforgery")) {
                dbg("csrf validation suspected; retrying once with fresh token");
                clearCsrfToken();
                return _invokeWithRetry(actionId, payload, options, false);
            }
        }

        let html = await safeText(res);
        const ms = performance.now() - started;

        // Apply OOB before normal swap (only on OK responses by default)
        if (res.ok) {
            const oob = processOob(html, options && options.sourceEl ? options.sourceEl : null);
            html = oob.html;
        }

        if (res.ok && targetEl) {
            setHtml(targetEl, html, swap);

            // If observeDom is off, newly injected triggers won't be wired unless we boot manually.
            if (!Heimdall.config.observeDom) {
                try {
                    boot(targetEl);
                } catch { /* ignore */ }
            }
        }

        const result = {
            ok: res.ok,
            status: res.status,
            html: res.ok ? html : null,
            error: res.ok ? null : html,
            response: res,
            ms
        };

        if (!res.ok) {
            emit("heimdall:error", { actionId, payload, target: targetEl, swap, status: res.status, body: html });
        }
        else {
            emit("heimdall:after", { actionId, payload, target: targetEl, swap, endpoint: url.toString(), status: res.status, ms, html });
        }

        if (typeof options.onSuccess === "function" && res.ok) {
            options.onSuccess(result);
        }

        if (typeof options.onError === "function" && !res.ok) {
            options.onError(result);
        }

        dbg("invoke <-", actionId, result);

        return result;
    }

    const DEFAULT_DISABLE_BY_TRIGGER = {
        load: false,
        click: true,
        change: false,
        input: false,
        submit: true,
        keydown: false,
        blur: false,
        hover: false,
        visible: false,
        scroll: false
    };

    function getCommonOptions(el) {
        const target = getAttr(el, "heimdall-content-target") || el;
        const swap = getAttr(el, "heimdall-content-swap") || "inner";
        const payload = payloadFromElement(el);
        return { target, swap, payload };
    }

    async function runActionFromElement(el, actionId, triggerName, extraOptions) {
        if (!el || !actionId)
            return;

        if (el.hasAttribute("disabled") || el.getAttribute("aria-disabled") === "true")
            return;

        const { target, swap, payload } = getCommonOptions(el);

        // default disable varies by trigger; attribute can override
        const defaultDisable = DEFAULT_DISABLE_BY_TRIGGER[triggerName] ?? false;
        const shouldDisable = truthyAttr(el, "heimdall-content-disable", defaultDisable);

        let wasDisabled = false;
        if (shouldDisable) {
            wasDisabled = el.hasAttribute("disabled");
            el.setAttribute("disabled", "disabled");
            el.setAttribute("aria-busy", "true");
        }

        // NOTE: pass sourceEl through so OOB allow-lists can make decisions
        const opts = Object.assign({ target, swap, fallbackTarget: el, sourceEl: el }, extraOptions || {});
        try {
            await invoke(actionId, payload, opts);
        } catch (err) {
            // eslint-disable-next-line no-console
            console.error(err);
        } finally {
            if (shouldDisable) {
                el.removeAttribute("aria-busy");
                if (!wasDisabled) el.removeAttribute("disabled");
            }
        }
    }

    function bootLoads(root) {
        const scope = root && isElement(root) ? root : document;
        const nodes = scope.querySelectorAll("[heimdall-content-load]");

        for (const el of nodes) {
            if (el.__heimdallLoaded)
                continue;
            el.__heimdallLoaded = true;

            const actionId = getAttr(el, "heimdall-content-load");
            if (!actionId)
                continue;

            runActionFromElement(el, actionId, "load").catch(() => { /* already logged */ });
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

                // if configured once, unobserve before invoking (prevents repeat)
                const once = truthyAttr(el, "heimdall-visible-once", true);
                if (once) {
                    try {
                        _visibleObserver.unobserve(el);
                    } catch {
                        /* ignore */
                    }
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
        const scope = root && isElement(root) ? root : document;
        const nodes = scope.querySelectorAll("[heimdall-content-visible]");

        const obs = ensureVisibleObserver();
        for (const el of nodes) {
            if (el.__heimdallVisibleBound)
                continue;
            el.__heimdallVisibleBound = true;

            // if someone wants re-arming, they can remove attribute and re-add;
            // this is intentionally simple.
            try {
                obs.observe(el);
            } catch {
                /* ignore */
            }
        }
    }

    const _scrollState = new WeakMap(); // el -> { ticking, lastFire }

    function isNearScrollEnd(el, thresholdPx) {
        // Allow both element scroll containers and the document scrolling element
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

            // rAF throttle
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
        const scope = root && isElement(root) ? root : document;
        const nodes = scope.querySelectorAll("[heimdall-content-scroll]");
        for (const el of nodes) {
            attachScroll(el);
        }
    }

    function boot(root) {
        bootLoads(root);
        bootVisible(root);
        bootScroll(root);
        bootPoll(root);
    }

    // Click handling (delegated)
    async function handleClick(e) {
        const el = e.target && e.target.closest
            ? e.target.closest("[heimdall-content-click]")
            : null;

        if (!el)
            return;
        if (e.defaultPrevented)
            return;

        if (e.button != null && e.button !== 0)
            return; // left click only
        if (e.metaKey || e.ctrlKey || e.shiftKey || e.altKey)
            return;

        const actionId = getAttr(el, "heimdall-content-click");
        if (!actionId)
            return;

        const isAnchor = el.tagName === "A";
        const preventDefault = truthyAttr(el, "heimdall-prevent-default", isAnchor);
        if (preventDefault)
            e.preventDefault();

        await runActionFromElement(el, actionId, "click");
    }

    // Change handling (delegated)
    async function handleChange(e) {
        const el = e.target && e.target.closest
            ? e.target.closest("[heimdall-content-change]")
            : null;

        if (!el)
            return;
        if (e.defaultPrevented)
            return;

        const actionId = getAttr(el, "heimdall-content-change");
        if (!actionId)
            return;

        // Optional debounce for change (uncommon, but useful for some widgets)
        const ms = intAttr(el, "heimdall-debounce", 0);
        if (ms > 0) {
            scheduleDebounced(el, "change", ms, () => {
                runActionFromElement(el, actionId, "change").catch(() => { /* logged */ });
            });
            return;
        }

        await runActionFromElement(el, actionId, "change");
    }

    // Input handling (delegated + debounced)
    const _debouncers = new WeakMap(); // el -> Map(key, timeoutId)

    function scheduleDebounced(el, key, ms, fn) {
        let map = _debouncers.get(el);
        if (!map) {
            map = new Map();
            _debouncers.set(el, map);
        }

        const prev = map.get(key);
        if (prev)
            clearTimeout(prev);

        const tid = setTimeout(() => {
            map.delete(key);
            fn();
        }, ms);

        map.set(key, tid);
    }

    async function handleInput(e) {
        const el = e.target && e.target.closest
            ? e.target.closest("[heimdall-content-input]")
            : null;

        if (!el)
            return;
        if (e.defaultPrevented)
            return;

        const actionId = getAttr(el, "heimdall-content-input");
        if (!actionId)
            return;

        // Default debounce for input unless user overrides
        const ms = intAttr(el, "heimdall-debounce", Heimdall.config.inputDebounceMs || 250);

        // For non-text-like inputs (e.g. range), still debounce but allow 0 
        if (ms > 0) {
            scheduleDebounced(el, "input", ms, () => {
                runActionFromElement(el, actionId, "input").catch(() => { /* logged */ });
            });
            return;
        }

        await runActionFromElement(el, actionId, "input");
    }

    // Submit handling (delegated)
    async function handleSubmit(e) {
        const form = e.target && e.target.closest
            ? e.target.closest("[heimdall-content-submit]")
            : null;

        if (!form)
            return;
        if (e.defaultPrevented)
            return;

        const actionId = getAttr(form, "heimdall-content-submit");
        if (!actionId)
            return;

        const preventDefault = truthyAttr(form, "heimdall-prevent-default", true);
        if (preventDefault)
            e.preventDefault();

        await runActionFromElement(form, actionId, "submit");
    }

    // Keydown handling (delegated)
    async function handleKeydown(e) {
        const el = e.target && e.target.closest
            ? e.target.closest("[heimdall-content-keydown]")
            : null;

        if (!el)
            return;
        if (e.defaultPrevented)
            return;

        const actionId = getAttr(el, "heimdall-content-keydown");
        if (!actionId)
            return;

        // If a key is specified, only fire when it matches
        const keySpec = getAttr(el, "heimdall-key");
        if (keySpec && !matchesKeySpec(e, keySpec))
            return;

        // By default: prevent default on Enter inside text-like inputs to avoid form submit + action double-fire
        const wantsPreventDefault = truthyAttr(
            el,
            "heimdall-prevent-default",
            (String(keySpec || "").toLowerCase() === "enter") && isTextLikeInput(e.target)
        );

        if (wantsPreventDefault)
            e.preventDefault();

        await runActionFromElement(el, actionId, "keydown");
    }

    // Blur handling (delegated via focusout, which bubbles)
    async function handleFocusOut(e) {
        const el = e.target && e.target.closest
            ? e.target.closest("[heimdall-content-blur]")
            : null;

        if (!el)
            return;
        if (e.defaultPrevented)
            return;

        const actionId = getAttr(el, "heimdall-content-blur");
        if (!actionId)
            return;

        await runActionFromElement(el, actionId, "blur");
    }

    // Hover handling (delegated via mouseover + relatedTarget check)
    const _hoverTimers = new WeakMap(); // el -> timeoutId

    function isRealMouseEnter(e, el) {
        // mouseover fires when moving within children; treat as enter only if coming from outside el
        const from = e.relatedTarget;
        return !(from && (from === el || (el.contains && el.contains(from))));
    }

    async function handleMouseOver(e) {
        const el = e.target && e.target.closest
            ? e.target.closest("[heimdall-content-hover]")
            : null;

        if (!el)
            return;
        if (e.defaultPrevented)
            return;
        if (!isRealMouseEnter(e, el))
            return;

        const actionId = getAttr(el, "heimdall-content-hover");
        if (!actionId)
            return;

        const delay = intAttr(el, "heimdall-hover-delay", Heimdall.config.hoverDelayMs || 150);

        // clear any prior pending hover invoke
        const prev = _hoverTimers.get(el);
        if (prev)
            clearTimeout(prev);

        if (delay > 0) {
            const tid = setTimeout(() => {
                _hoverTimers.delete(el);
                runActionFromElement(el, actionId, "hover").catch(() => { /* logged */ });
            }, delay);
            _hoverTimers.set(el, tid);
            return;
        }

        await runActionFromElement(el, actionId, "hover");
    }

    // Cancel hover invoke if leaving before delay
    async function handleMouseOut(e) {
        const el = e.target && e.target.closest
            ? e.target.closest("[heimdall-content-hover]")
            : null;

        if (!el)
            return;

        // Only treat as leaving the element if we're going to somewhere outside
        const to = e.relatedTarget;
        if (to && (to === el || (el.contains && el.contains(to))))
            return;

        const tid = _hoverTimers.get(el);
        if (tid) {
            clearTimeout(tid);
            _hoverTimers.delete(el);
        }
    }

    const _pollState = new WeakMap(); // el -> { timerId, inFlight }

    function attachPoll(el) {
        if (el.__heimdallPollBound)
            return;
        el.__heimdallPollBound = true;

        const intervalMs = intAttr(el, "heimdall-poll", 0);
        if (!intervalMs || intervalMs <= 0)
            return;

        // We reuse the load action to keep MVP simple.
        const actionId = getAttr(el, "heimdall-content-load");
        if (!actionId) {
            if (Heimdall.config.debug) {
                // eslint-disable-next-line no-console
                console.warn(`[Heimdall ${VERSION}] heimdall-poll set but no heimdall-content-load found.`, el);
            }
            return;
        }

        const state = { timerId: null, inFlight: false };
        _pollState.set(el, state);

        const tick = async () => {
            // Stop if element no longer exists in DOM (covers swaps/removals)
            if (!el.isConnected) {
                stopPoll(el);
                return;
            }

            // stop if tab not in view
            if (document.hidden)
                return;

            // no overlap
            if (state.inFlight)
                return;

            state.inFlight = true;
            try {
                await runActionFromElement(el, actionId, "load", { reason: "poll" });
            } finally {
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
                } catch {
                    // runActionFromElement already logs
                } finally {
                    schedule(); // keep looping
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
        const scope = root && isElement(root) ? root : document;
        const nodes = scope.querySelectorAll("[heimdall-poll]");
        for (const el of nodes) {
            attachPoll(el);
        }
    }


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

            for (const node of nodes) {
                boot(node);
            }
        }

        function scheduleFlush() {
            if (scheduled)
                return;
            scheduled = true;

            // microtask batch
            Promise.resolve().then(flush);
        }

        const obs = new MutationObserver((mutations) => {
            for (const m of mutations) {
                for (const n of m.addedNodes) {
                    if (!n || n.nodeType !== 1)
                        continue; // element only
                    pending.add(n);
                }
            }
            if (pending.size)
                scheduleFlush();
        });

        obs.observe(document.body, { childList: true, subtree: true });
        Heimdall._observer = obs;

        dbg("MutationObserver installed");
    }

    // Public API
    const Heimdall = {
        version: VERSION,
        apiVersion: API_VERSION,

        invoke,
        boot,
        onReady,
        clearCsrfToken,

        // Internal (not promised): for diagnostics
        _observer: null,

        config: {
            basePath: DEFAULT_BASE_PATH,
            apiVersion: API_VERSION,

            endpoints: {
                contentActions: DEFAULT_CONTENT_ENDPOINT,
                csrf: DEFAULT_CSRF_ENDPOINT
            },

            observeDom: true,
            debug: false,
            credentials: "same-origin",

            // Trigger tuning
            inputDebounceMs: 250,
            hoverDelayMs: 150,
            scrollThresholdPx: 120,
            scrollMinIntervalMs: 250,

            // Visible tuning
            visibleRootMargin: "0px",
            visibleThreshold: 0,

            // OOB (Out-of-band swaps)
            oobEnabled: true,

            // If null => allow all OOB targets.
            // Recommended for prod: allowlist “outlets”, e.g. ["#modalHost", "#toastHost"].
            // You can also supply a function: (selector, sourceEl) => boolean
            oobAllowedTargets: null
        }
    };

    global.Heimdall = Heimdall;

    // Startup
    onReady(() => {
        // Delegated listeners (install once)
        if (!document.__heimdallDelegatesInstalled) {
            document.__heimdallDelegatesInstalled = true;

            // capture phase for click helps with nested handlers;
            document.addEventListener("click", handleClick, true);

            // bubble phase is fine for the rest
            document.addEventListener("change", handleChange, false);
            document.addEventListener("input", handleInput, false);
            document.addEventListener("submit", handleSubmit, false);
            document.addEventListener("keydown", handleKeydown, false);

            // focusout bubbles (blur does not)
            document.addEventListener("focusout", handleFocusOut, false);

            // mouseover/out bubble (mouseenter/leave do not)
            document.addEventListener("mouseover", handleMouseOver, false);
            document.addEventListener("mouseout", handleMouseOut, false);
        }

        // initial scan
        boot(document);

        // auto-wire future swaps
        installObserver();

        // Blazor Enhanced Navigation support
        if (global.Blazor && typeof global.Blazor.addEventListener === "function") {
            global.Blazor.addEventListener("enhancedload", () => {
                boot(document);
            });
        }
    });

})(window);
