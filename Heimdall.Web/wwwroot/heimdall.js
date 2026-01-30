(function (global) {
    "use strict";

    // ============================================================
    // Heimdall.js (v1)
    // - Content actions: POST /__heimdall/v1/content/actions
    // - CSRF token:      GET  /__heimdall/v1/security/csrf
    // - Attributes:
    //   * heimdall-content-load="Action.Id"
    //   * heimdall-content-click="Action.Id"
    //   * heimdall-content-target="#selector" (or element)
    //   * heimdall-content-swap="inner|outer|beforeend|afterbegin"
    //   * heimdall-content-disable="true|false"
    //   * heimdall-prevent-default="true|false" (esp anchors)
    //   * heimdall-payload='{"json":1}'
    //   * heimdall-payload-from="closest-form|self|#formSelector|ref:Path.To.Obj"
    //   * heimdall-payload-ref="Path.To.Obj"
    // ============================================================

    const VERSION = "1.0.0";
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

    // Invoke (content action)
    async function invoke(actionId, payload, options) {
        return _invokeWithRetry(actionId, payload, options, true);
    }

    async function _invokeWithRetry(actionId, payload, options, shouldRetry) {
        options = options || {};

        const endpoint = options.endpoint || Heimdall.config.endpoints.contentActions;
        const targetEl = resolveTarget(options.target, options.fallbackTarget || null);
        const swap = options.swap || "inner";
        const credentials = options.credentials || Heimdall.config.credentials || "same-origin";

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

        // Payload can be an object or null. We JSON.stringify it.
        let body = "{}";
        try {
            body = payload == null ? "{}" : JSON.stringify(payload);
        } catch (e) {
            // JSON stringify can fail (circular refs). Provide a good error.
            const err = new Error(`Heimdall payload is not JSON-serializable for action '${actionId}'.`);
            err.cause = e;
            emit("heimdall:error", { actionId, payload, target: targetEl, swap, status: 0, error: err });
            throw err;
        }

        const started = (global.performance && performance.now) ? performance.now() : Date.now();
        emit("heimdall:before", { actionId, payload, target: targetEl, swap, endpoint });

        dbg("invoke ->", actionId, { endpoint, swap, target: targetEl });

        const res = await fetch(endpoint, {
            method: "POST",
            headers,
            body,
            credentials
        });

        // Retry once if we *likely* failed CSRF validation.
        if (res.status === 400 && shouldRetry) {
            const text = await safeText(res);
            const lower = (text || "").toLowerCase();
            if (lower.includes("antiforgery") || lower.includes("csrf") || lower.includes("validation")) {
                dbg("csrf validation suspected; retrying once with fresh token");
                clearCsrfToken();
                return _invokeWithRetry(actionId, payload, options, false);
            }
        }

        if (!res.ok) {
            const text = await safeText(res);
            const err = new Error(`Heimdall failed (${res.status}): ${text || res.statusText}`);
            err.status = res.status;
            err.body = text;

            emit("heimdall:error", { actionId, payload, target: targetEl, swap, status: res.status, body: text, error: err });
            dbg("invoke <- ERROR", actionId, res.status, text || res.statusText);
            throw err;
        }

        const html = await res.text();

        if (targetEl) {
            setHtml(targetEl, html, swap);
        } else if (Heimdall.config.debug) {
            // eslint-disable-next-line no-console
            console.warn(`[Heimdall ${VERSION}] Target not found for action '${actionId}'. HTML returned but not swapped.`);
        }

        const ended = (global.performance && performance.now) ? performance.now() : Date.now();
        const ms = Math.max(0, ended - started);

        emit("heimdall:after", { actionId, payload, target: targetEl, swap, endpoint, status: res.status, ms, html });

        if (typeof options.onSuccess === "function") {
            options.onSuccess({ html, target: targetEl, response: res, ms });
        }

        dbg("invoke <-", actionId, { status: res.status, ms });

        return html;
    }

    // Boot: auto-load scanning (subtree-friendly)
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

            const target = getAttr(el, "heimdall-content-target") || el;
            const swap = getAttr(el, "heimdall-content-swap") || "inner";
            const payload = payloadFromElement(el);

            invoke(actionId, payload, { target, swap, fallbackTarget: el })
                .catch(err => {
                    // eslint-disable-next-line no-console
                    console.error(err);
                });
        }
    }

    function boot(root) {
        bootLoads(root);
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

        if (el.hasAttribute("disabled") || el.getAttribute("aria-disabled") === "true")
            return;

        const actionId = getAttr(el, "heimdall-content-click");
        if (!actionId)
            return;

        const isAnchor = el.tagName === "A";
        const preventDefault = truthyAttr(el, "heimdall-prevent-default", isAnchor);
        if (preventDefault) e.preventDefault();

        const target = getAttr(el, "heimdall-content-target") || el;
        const swap = getAttr(el, "heimdall-content-swap") || "inner";
        const payload = payloadFromElement(el);

        const shouldDisable = truthyAttr(el, "heimdall-content-disable", true);

        let wasDisabled = false;
        if (shouldDisable) {
            wasDisabled = el.hasAttribute("disabled");
            el.setAttribute("disabled", "disabled");
            el.setAttribute("aria-busy", "true");
        }

        try {
            await invoke(actionId, payload, { target, swap, fallbackTarget: el });
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

    // MutationObserver: auto boot for swapped-in DOM
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
            if (pending.size) scheduleFlush();
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
            credentials: "same-origin"
        }
    };

    global.Heimdall = Heimdall;

    // Startup
    onReady(() => {
        if (!document.__heimdallClickInstalled) {
            document.__heimdallClickInstalled = true;
            document.addEventListener("click", handleClick, true);
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
