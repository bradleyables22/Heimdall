(function (global) {
    "use strict";

    // ============================================================================
    // Heimdall.js
    // ---------------------------------------------------------------------------
    // Version: 1.0.0
    // API Version: v1
    // ---------------------------------------------------------------------------
    // Endpoints
    // ---------------------------------------------------------------------------
    // Content Actions:
    //   POST /__heimdall/v1/content/actions
    //     - Executes a server action
    //     - Returns HTML (with optional <invocation> directives)
    //
    // CSRF Token:
    //   GET  /__heimdall/v1/csrf
    //     - Returns antiforgery token
    //     - Cached client-side and reused automatically
    //
    // Bifrost (Server-Sent Events):
    //   GET  /__heimdall/v1/bifrost?topic=...
    //     - Subscribes to a server topic
    //     - Streams HTML payloads and/or <invocation> directives
    //     - Protected via short-lived subscribe token (minted with CSRF)
    //
    // ---------------------------------------------------------------------------
    // Content Action Attributes
    // ---------------------------------------------------------------------------
    // Triggers:
    //   - heimdall-content-load="Action.Id"
    //   - heimdall-content-click="Action.Id"
    //   - heimdall-content-change="Action.Id"
    //   - heimdall-content-input="Action.Id"
    //   - heimdall-content-submit="Action.Id"
    //   - heimdall-content-keydown="Action.Id"
    //   - heimdall-content-blur="Action.Id"
    //   - heimdall-content-hover="Action.Id"
    //   - heimdall-content-visible="Action.Id"
    //   - heimdall-content-scroll="Action.Id"
    //
    // Common Options:
    //   - heimdall-content-target="#selector"
    //   - heimdall-content-swap="inner|outer|beforeend|afterbegin|none"
    //   - heimdall-content-disable="true|false"
    //   - heimdall-prevent-default="true|false"
    //
    // Payload Options:
    //   - heimdall-payload='{"json":1}'
    //   - heimdall-payload-from="closest-form|self|#form|ref:path|closest-state[:key]"
    //   - heimdall-payload-ref="Path.To.Object"
    //
    // Trigger Modifiers:
    //   - heimdall-debounce="ms"
    //   - heimdall-key="Enter|Escape|13"
    //   - heimdall-hover-delay="ms"
    //   - heimdall-visible-once="true|false"
    //   - heimdall-scroll-threshold="px"
    //   - heimdall-poll="ms"
    //
    // ---------------------------------------------------------------------------
    // Out-of-Band Updates (<invocation>)
    // ---------------------------------------------------------------------------
    // Any <invocation> element returned by the server is treated as an instruction
    // and is never rendered directly into the response output.
    //
    // Required:
    //   - heimdall-content-target="#selector"
    //
    // Optional:
    //   - heimdall-content-swap="inner|outer|beforeend|afterbegin|none"
    //
    // Payload:
    //   - Wrap HTML fragments in <template> to preserve table rows (<tr>, etc)
    //
    // Security:
    //   - <script> tags are always stripped
    //   - Invocation targets can be allow-listed via config
    //
    // ---------------------------------------------------------------------------
    // Bifrost (SSE) Attributes
    // ---------------------------------------------------------------------------
    //   - heimdall-sse="topic:name"
    //   - heimdall-sse-topic="topic:name"      (alias)
    //   - heimdall-sse-target="#selector"       (default: element itself)
    //   - heimdall-sse-swap="inner|outer|beforeend|afterbegin|none"
    //   - heimdall-sse-event="heimdall"
    //   - heimdall-sse-disable="true|false"
    //
    // Bifrost notes:
    //   - Uses EventSource (GET only)
    //   - Automatically reconnects
    //   - Designed for same-origin use
    //   - Subscription access is gated server-side
    //
    // ---------------------------------------------------------------------------
    // This file is intentionally dependency-free and framework-agnostic.
    // It is safe to use alongside Blazor, Razor Pages, MVC, or static HTML.
    //
    // ============================================================================

    const VERSION = "1.3.1";
    const API_VERSION = 1;

    const DEFAULT_BASE_PATH = "/__heimdall";
    const DEFAULT_CONTENT_ENDPOINT = `${DEFAULT_BASE_PATH}/v${API_VERSION}/content/actions`;
    const DEFAULT_CSRF_ENDPOINT = `${DEFAULT_BASE_PATH}/v${API_VERSION}/csrf`;
    const DEFAULT_BIFROST_ENDPOINT = `${DEFAULT_BASE_PATH}/v${API_VERSION}/bifrost`;
    const DEFAULT_BIFROST_TOKEN_ENDPOINT = `${DEFAULT_BASE_PATH}/v${API_VERSION}/bifrost/token`;

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
        }
        catch {
            return null;
        }
    }

    async function safeText(res) {
        try {
            return await res.text();
        }
        catch {
            return "";
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

    // ============================================================
    // NEW: Closest "state" payload support
    // ------------------------------------------------------------
    // Usage:
    //   heimdall-payload-from="closest-state"
    //     -> reads nearest ancestor with data-heimdall-state='{"..."}'
    //
    //   heimdall-payload-from="closest-state:filters"
    //     -> reads nearest ancestor with data-heimdall-state-filters='{"..."}'
    //
    // Notes:
    // - This is purely client-side state (DOM as state store).
    // - It does not execute scripts; it only JSON-parses attribute values.
    // ============================================================

    function findClosestStateElement(el, key) {
        let cur = el;
        while (cur && cur.nodeType === 1) {
            if (key) {
                const attr = `data-heimdall-state-${key}`;
                if (cur.hasAttribute && cur.hasAttribute(attr))
                    return cur;
            } else {
                if (cur.hasAttribute && cur.hasAttribute("data-heimdall-state"))
                    return cur;
            }
            cur = cur.parentElement;
        }
        return null;
    }

    function readClosestState(el, key) {
        const host = findClosestStateElement(el, key);
        if (!host)
            return null;

        const attr = key ? `data-heimdall-state-${key}` : "data-heimdall-state";
        const raw = host.getAttribute(attr);
        if (!raw)
            return null;

        return safeJsonParse(raw);
    }

    function resolvePayloadRef(el) {
        const ref = getAttr(el, "heimdall-payload-ref");
        if (ref)
            return getByPath(global, ref);

        const from = (getAttr(el, "heimdall-payload-from") || "").trim();
        if (from.toLowerCase().startsWith("ref:")) {
            const path = from.substring(4).trim();
            return getByPath(global, path);
        }
        return undefined;
    }

    function payloadFromElement(el) {
        const payloadAttr = getAttr(el, "heimdall-payload");
        if (payloadAttr)
            return safeJsonParse(payloadAttr);

        const refObj = resolvePayloadRef(el);
        if (refObj !== undefined)
            return refObj;

        const fromRaw = (getAttr(el, "heimdall-payload-from") || "").trim();
        const from = fromRaw.toLowerCase();

        // NEW: closest-state[:key]
        if (from === "closest-state" || from.startsWith("closest-state:")) {
            const key = from.startsWith("closest-state:") ? fromRaw.substring("closest-state:".length).trim() : null;
            return readClosestState(el, key || null);
        }

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
            for (const key in el.dataset) obj[key] = el.dataset[key];
            return obj;
        }

        const form = document.querySelector(fromRaw);
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

    // ============================================================
    // DOM-first swapping + HTML parsing
    // ============================================================

    function stripScripts(rootNode) {
        if (!rootNode || !rootNode.querySelectorAll)
            return;
        const scripts = rootNode.querySelectorAll("script");
        for (const s of scripts)
            s.remove();
    }

    function parseHtmlToTemplate(html) {
        const tpl = document.createElement("template");
        tpl.innerHTML = html || "";
        stripScripts(tpl.content);
        return tpl;
    }

    function fragmentToNodesArray(fragment) {
        return Array.from(fragment.childNodes || []);
    }

    function applySwap(targetEl, fragment, swap) {
        const mode = (swap || "inner").toLowerCase();

        if (mode === "none")
            return { didApply: false, appliedRoot: null };
        if (!targetEl)
            return { didApply: false, appliedRoot: null };

        const nodes = fragmentToNodesArray(fragment);
        const firstElement = nodes.find(n => n && n.nodeType === 1) || null;
        const appliedRoot = firstElement || targetEl;

        switch (mode) {
            case "outer": {
                if (nodes.length === 0) {
                    targetEl.remove();
                    return { didApply: true, appliedRoot: targetEl.parentElement || null };
                }
                targetEl.replaceWith(...nodes);
                return { didApply: true, appliedRoot };
            }
            case "beforeend":
                targetEl.append(...nodes);
                return { didApply: true, appliedRoot };
            case "afterbegin":
                targetEl.prepend(...nodes);
                return { didApply: true, appliedRoot };
            default:
                targetEl.replaceChildren(...nodes);
                return { didApply: true, appliedRoot };
        }
    }

    function stripInvocationsFromFragment(fragment) {
        if (!fragment || !fragment.querySelectorAll)
            return;
        const invs = fragment.querySelectorAll("invocation");
        for (const inv of invs)
            inv.remove();
    }

    function fragmentToHtml(fragment) {
        const host = document.createElement("div");
        try {
            host.append(fragment.cloneNode(true));
        } catch {
            const frag = fragment && fragment.cloneNode ? fragment.cloneNode(true) : null;
            if (frag && frag.childNodes)
                host.append(...Array.from(frag.childNodes));
        }
        return host.innerHTML;
    }

    function sanitizeHtmlStringNoApply(html) {
        if (!html)
            return html;

        const hasScript = html.indexOf("<script") !== -1 || html.indexOf("<SCRIPT") !== -1;
        const hasInv = html.indexOf("<Invocation") !== -1 || html.indexOf("<invocation") !== -1;
        if (!hasScript && !hasInv)
            return html;

        const tpl = parseHtmlToTemplate(html);
        stripInvocationsFromFragment(tpl.content);
        return fragmentToHtml(tpl.content);
    }

    function processOob(html, sourceEl) {
        const hasInv = html && (html.indexOf("<Invocation") !== -1 || html.indexOf("<invocation") !== -1);
        const hasScript = html && (html.indexOf("<script") !== -1 || html.indexOf("<SCRIPT") !== -1);

        if (!hasInv && !hasScript)
            return { html: html || "", applied: 0 };

        const tpl = parseHtmlToTemplate(html);
        const fragment = tpl.content;

        const invocations = fragment.querySelectorAll("invocation");
        if (!invocations || invocations.length === 0) {
            return { html: fragmentToHtml(fragment), applied: 0 };
        }

        if (!Heimdall.config.oobEnabled) {
            stripInvocationsFromFragment(fragment);
            return { html: fragmentToHtml(fragment), applied: 0 };
        }

        let applied = 0;

        for (const invEl of Array.from(invocations)) {
            const targetSel = getAttr(invEl, "heimdall-content-target");
            if (!targetSel) {
                dbg("Invocation missing heimdall-content-target; stripping", invEl);
                invEl.remove();
                continue;
            }

            if (!matchesAllowedTarget(targetSel, sourceEl)) {
                if (Heimdall.config.debug) {
                    // eslint-disable-next-line no-console
                    console.warn(`[Heimdall ${VERSION}] OOB target not allowed: '${targetSel}'.`);
                }
                invEl.remove();
                continue;
            }

            const swap = (getAttr(invEl, "heimdall-content-swap") || "inner").toLowerCase();
            const targetEl = resolveTarget(targetSel, null);

            if (!targetEl) {
                dbg("Invocation target not found; stripping", targetSel);
                invEl.remove();
                continue;
            }

            if (swap !== "none") {
                const payloadTemplate = invEl.querySelector("template");

                let payloadFrag;
                if (payloadTemplate && payloadTemplate.content) {
                    payloadFrag = payloadTemplate.content.cloneNode(true);
                } else {
                    payloadFrag = parseHtmlToTemplate(invEl.innerHTML || "").content;
                }

                stripScripts(payloadFrag);

                const { didApply, appliedRoot } = applySwap(targetEl, payloadFrag, swap);
                if (didApply) {
                    applied++;
                    if (!Heimdall.config.observeDom) {
                        try {
                            boot(appliedRoot || targetEl);
                        }
                        catch { /* ignore */ }
                    }
                }
            }

            invEl.remove();
        }

        return { html: fragmentToHtml(fragment), applied };
    }

    // ============================================================
    // CSRF token caching
    // ============================================================

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
                    credentials: "same-origin",
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });

                if (!res.ok)
                    throw new Error(`CSRF token fetch failed: ${res.status}`);

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
        _bifrostTokenByTopic.clear();
        _bifrostTokenPromiseByTopic.clear();
    }

    function matchesAllowedTarget(selector, sourceEl) {
        const rule = Heimdall.config.oobAllowedTargets;

        if (!rule)
            return true;

        if (Array.isArray(rule))
            return rule.includes(selector);

        if (typeof rule === "function") {
            try {
                return !!rule(selector, sourceEl);
            }
            catch {
                return false;
            }
        }

        return true;
    }

    // ============================================================
    // Bifrost subscribe token (Option 2)
    // ============================================================

    const _bifrostTokenByTopic = new Map();          // topic -> { token, expiresAtMs }
    const _bifrostTokenPromiseByTopic = new Map();   // topic -> Promise<string>

    async function ensureBifrostSubscribeToken(topic) {
        const t = String(topic || "").trim();
        if (!t)
            throw new Error("Bifrost topic is required.");

        // cached + still valid?
        const cached = _bifrostTokenByTopic.get(t);
        if (cached && cached.token && cached.expiresAtMs && Date.now() < cached.expiresAtMs) {
            return cached.token;
        }

        // in-flight?
        const inflight = _bifrostTokenPromiseByTopic.get(t);
        if (inflight)
            return inflight;

        const p = (async () => {
            try {
                const csrf = await ensureCsrfToken();

                const base = Heimdall.config.endpoints && Heimdall.config.endpoints.bifrostToken
                    ? Heimdall.config.endpoints.bifrostToken
                    : DEFAULT_BIFROST_TOKEN_ENDPOINT;

                const url = new URL(base, global.location?.origin || undefined);
                url.searchParams.set("topic", t);

                const res = await fetch(url.toString(), {
                    method: "GET",
                    credentials: "same-origin",
                    headers: {
                        "X-Requested-With": "XMLHttpRequest",
                        [CSRF_HEADER]: csrf
                    }
                });

                if (!res.ok) {
                    const body = await safeText(res);
                    throw new Error(`Bifrost token fetch failed: ${res.status}. ${body || ""}`.trim());
                }

                const data = await res.json();
                const token = data && (data.token || data.st);
                const expiresInSeconds = data && (data.expiresInSeconds || data.expires_in_seconds || 120);

                if (!token)
                    throw new Error("Bifrost token response missing token.");

                // cache with a safety margin (expire early)
                const ttlMs = Math.max(5, parseInt(expiresInSeconds, 10) || 120) * 1000;
                const expiresAtMs = Date.now() + Math.max(5000, ttlMs - 5000);

                _bifrostTokenByTopic.set(t, { token, expiresAtMs });
                return token;
            } finally {
                _bifrostTokenPromiseByTopic.delete(t);
            }
        })();

        _bifrostTokenPromiseByTopic.set(t, p);
        return p;
    }

    // ============================================================
    // Content Actions
    // ============================================================

    async function invoke(actionId, payload, options) {
        return _invokeWithRetry(actionId, payload, options, true);
    }

    async function _invokeWithRetry(actionId, payload, options, shouldRetry) {
        options = options || {};

        const endpointBase = options.endpoint || Heimdall.config.endpoints.contentActions;
        const targetEl = resolveTarget(options.target, options.fallbackTarget || null);
        const swap = options.swap || "inner";

        const url = new URL(endpointBase, global.location?.origin || undefined);
        url.searchParams.set("action", actionId);

        const token = await ensureCsrfToken();

        const headers = {
            "Content-Type": "application/json",
            [ACTION_HEADER]: actionId,
            [CSRF_HEADER]: token
        };

        if (options.headers) {
            for (const k in options.headers) headers[k] = options.headers[k];
        }

        let body = "{}";
        try {
            body = payload == null ? "{}" : JSON.stringify(payload);
        } catch (e) {
            const err = new Error(`Heimdall payload is not JSON-serializable for action '${actionId}'.`);
            err.cause = e;
            emit("heimdall:error", { actionId, payload, target: targetEl, swap, status: 0, error: err });
            throw err;
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
                credentials: "same-origin"
            });
        } catch (networkErr) {
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

        if (res.ok) {
            const oob = processOob(html, options && options.sourceEl ? options.sourceEl : null);
            html = oob.html;
        } else {
            html = sanitizeHtmlStringNoApply(html);
        }

        if (res.ok && targetEl) {
            const mainTpl = parseHtmlToTemplate(html);
            stripInvocationsFromFragment(mainTpl.content);

            const { didApply, appliedRoot } = applySwap(targetEl, mainTpl.content, swap);

            if (didApply && !Heimdall.config.observeDom) {
                try {
                    boot(appliedRoot || targetEl);
                }
                catch { /* ignore */ }
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
        } else {
            emit("heimdall:after", { actionId, payload, target: targetEl, swap, endpoint: url.toString(), status: res.status, ms, html });
        }

        if (typeof options.onSuccess === "function" && res.ok)
            options.onSuccess(result);
        if (typeof options.onError === "function" && !res.ok)
            options.onError(result);

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
        scroll: false,
        sse: false
    };

    function getCommonOptions(el, triggerName) {
        const target = getAttr(el, "heimdall-content-target") || el;
        const swap = getAttr(el, "heimdall-content-swap") || "inner";

        let payload = payloadFromElement(el);
        if ((payload == null) && triggerName === "submit") {
            if (el && el.tagName === "FORM") {
                payload = formDataToObject(new FormData(el));
            } else {
                const form = el.closest && el.closest("form");
                if (form)
                    payload = formDataToObject(new FormData(form));
            }
        }

        return { target, swap, payload };
    }

    async function runActionFromElement(el, actionId, triggerName, extraOptions) {
        if (!el || !actionId)
            return;
        if (el.hasAttribute("disabled") || el.getAttribute("aria-disabled") === "true")
            return;

        const { target, swap, payload } = getCommonOptions(el, triggerName);

        const defaultDisable = DEFAULT_DISABLE_BY_TRIGGER[triggerName] ?? false;
        const shouldDisable = truthyAttr(el, "heimdall-content-disable", defaultDisable);

        let wasDisabled = false;
        if (shouldDisable) {
            wasDisabled = el.hasAttribute("disabled");
            el.setAttribute("disabled", "disabled");
            el.setAttribute("aria-busy", "true");
        }

        const opts = Object.assign({ target, swap, fallbackTarget: el, sourceEl: el }, extraOptions || {});
        try {
            await invoke(actionId, payload, opts);
        }
        catch (err) {
            // eslint-disable-next-line no-console
            console.error(err);
        } finally {
            if (shouldDisable) {
                el.removeAttribute("aria-busy");
                if (!wasDisabled)
                    el.removeAttribute("disabled");
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

            runActionFromElement(el, actionId, "load").catch(() => { /* logged */ });
        }
    }

    // ============================================================
    // Visible trigger
    // ============================================================

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
        const scope = root && isElement(root) ? root : document;
        const nodes = scope.querySelectorAll("[heimdall-content-visible]");

        const obs = ensureVisibleObserver();
        for (const el of nodes) {
            if (el.__heimdallVisibleBound)
                continue;
            el.__heimdallVisibleBound = true;

            try {
                obs.observe(el);
            }
            catch { /* ignore */ }
        }
    }

    // ============================================================
    // Scroll trigger
    // ============================================================

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
        const scope = root && isElement(root) ? root : document;
        const nodes = scope.querySelectorAll("[heimdall-content-scroll]");
        for (const el of nodes)
            attachScroll(el);
    }

    // ============================================================
    // Polling
    // ============================================================

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
            if (Heimdall.config.debug) {
                // eslint-disable-next-line no-console
                console.warn(`[Heimdall ${VERSION}] heimdall-poll set but no heimdall-content-load found.`, el);
            }
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
        const scope = root && isElement(root) ? root : document;
        const nodes = scope.querySelectorAll("[heimdall-poll]");
        for (const el of nodes)
            attachPoll(el);
    }

    // ============================================================
    // Bifrost SSE
    // ============================================================

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

        try {
            const oob = processOob(html, state.el);
            html = oob.html;
        } catch (e) {
            emit("heimdall:sse-error", { topic: state.topic, url: state.url, el: state.el, error: e });
            if (Heimdall.config.debug) {
                // eslint-disable-next-line no-console
                console.error(`[Heimdall ${VERSION}] SSE OOB processing error`, e);
            }
            return;
        }

        if (swapMode !== "none" && targetEl) {
            const mainTpl = parseHtmlToTemplate(html);
            stripInvocationsFromFragment(mainTpl.content);

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
                console.warn(`[Heimdall ${VERSION}] EventSource not available; SSE disabled.`, el);
            }
            return;
        }

        const eventName = (getAttr(el, "heimdall-sse-event") || Heimdall.config.sseEventName || "heimdall").trim();
        const target = getAttr(el, "heimdall-sse-target") || el;
        const swap = (getAttr(el, "heimdall-sse-swap") || Heimdall.config.sseDefaultSwap || "none").toLowerCase();

        const state = {
            el,
            topic,
            url: null,           // set after token minted
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

        // Token mint + connect (async) so the developer never deals with it.
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
                        console.error(`[Heimdall ${VERSION}] SSE connect failed`, e);
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
                        console.warn(`[Heimdall ${VERSION}] SSE error (auto-reconnect expected)`, { topic, url }, e);
                    }
                };
            } catch (e) {
                emit("heimdall:sse-error", { topic, url: state.url, el, error: e });
                if (Heimdall.config.debug) {
                    // eslint-disable-next-line no-console
                    console.error(`[Heimdall ${VERSION}] SSE token/connect failed`, e);
                }
                closeSseState(state, "token-failed");
            }
        })();
    }

    function bootSse(root) {
        const scope = root && isElement(root) ? root : document;
        const nodes = scope.querySelectorAll("[heimdall-sse],[heimdall-sse-topic]");
        for (const el of nodes)
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

    // Programmatic API
    function sseConnect(topic, options) {
        options = options || {};
        const el = options.element || document.body;

        if (!isElement(el))
            throw new Error("Heimdall.sse.connect requires an element (options.element).");

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

    // ============================================================
    // boot()
    // ============================================================

    function boot(root) {
        bootLoads(root);
        bootVisible(root);
        bootScroll(root);
        bootPoll(root);
        bootSse(root);
    }

    // ============================================================
    // Delegated handlers
    // ============================================================

    async function handleClick(e) {
        const el = e.target && e.target.closest ? e.target.closest("[heimdall-content-click]") : null;
        if (!el)
            return;
        if (e.defaultPrevented)
            return;
        if (e.button != null && e.button !== 0)
            return;
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

    async function handleChange(e) {
        const el = e.target && e.target.closest ? e.target.closest("[heimdall-content-change]") : null;
        if (!el)
            return;
        if (e.defaultPrevented)
            return;

        const actionId = getAttr(el, "heimdall-content-change");
        if (!actionId)
            return;

        const ms = intAttr(el, "heimdall-debounce", 0);
        if (ms > 0) {
            scheduleDebounced(el, "change", ms, () => {
                runActionFromElement(el, actionId, "change").catch(() => { /* logged */ });
            });
            return;
        }

        await runActionFromElement(el, actionId, "change");
    }

    const _debouncers = new WeakMap();

    function scheduleDebounced(el, key, ms, fn) {
        let map = _debouncers.get(el);
        if (!map) {
            map = new Map();
            _debouncers.set(el, map);
        }

        const prev = map.get(key);
        if (prev) clearTimeout(prev);

        const tid = setTimeout(() => {
            map.delete(key);
            fn();
        }, ms);

        map.set(key, tid);
    }

    async function handleInput(e) {
        const el = e.target && e.target.closest ? e.target.closest("[heimdall-content-input]") : null;
        if (!el)
            return;
        if (e.defaultPrevented)
            return;

        const actionId = getAttr(el, "heimdall-content-input");
        if (!actionId)
            return;

        const ms = intAttr(el, "heimdall-debounce", Heimdall.config.inputDebounceMs || 250);

        if (ms > 0) {
            scheduleDebounced(el, "input", ms, () => {
                runActionFromElement(el, actionId, "input").catch(() => { /* logged */ });
            });
            return;
        }

        await runActionFromElement(el, actionId, "input");
    }

    async function handleSubmit(e) {
        const form = e.target && e.target.closest ? e.target.closest("[heimdall-content-submit]") : null;
        if (!form)
            return;
        if (e.defaultPrevented)
            return;

        const actionId = getAttr(form, "heimdall-content-submit");
        if (!actionId)
            return;

        const preventDefault = truthyAttr(form, "heimdall-prevent-default", true);
        if (preventDefault) e.preventDefault();

        await runActionFromElement(form, actionId, "submit");
    }

    function normalizeKeySpec(spec) {
        return String(spec || "").trim();
    }

    function matchesKeySpec(e, spec) {
        const s = normalizeKeySpec(spec);
        if (!s)
            return true;

        if (/^\d+$/.test(s)) {
            const code = parseInt(s, 10);
            const kc = (e.keyCode != null ? e.keyCode : e.which);
            return kc === code;
        }

        return String(e.key || "").toLowerCase() === s.toLowerCase();
    }

    async function handleKeydown(e) {
        const el = e.target && e.target.closest ? e.target.closest("[heimdall-content-keydown]") : null;
        if (!el)
            return;
        if (e.defaultPrevented)
            return;

        const actionId = getAttr(el, "heimdall-content-keydown");
        if (!actionId)
            return;

        const keySpec = getAttr(el, "heimdall-key");
        if (keySpec && !matchesKeySpec(e, keySpec))
            return;

        const wantsPreventDefault = truthyAttr(
            el,
            "heimdall-prevent-default",
            (String(keySpec || "").toLowerCase() === "enter") &&
            (e.target && (e.target.tagName === "INPUT" || e.target.tagName === "TEXTAREA"))
        );

        if (wantsPreventDefault) e.preventDefault();

        await runActionFromElement(el, actionId, "keydown");
    }

    async function handleFocusOut(e) {
        const el = e.target && e.target.closest ? e.target.closest("[heimdall-content-blur]") : null;
        if (!el)
            return;
        if (e.defaultPrevented)
            return;

        const actionId = getAttr(el, "heimdall-content-blur");
        if (!actionId)
            return;

        await runActionFromElement(el, actionId, "blur");
    }

    const _hoverTimers = new WeakMap();

    function isRealMouseEnter(e, el) {
        const from = e.relatedTarget;
        return !(from && (from === el || (el.contains && el.contains(from))));
    }

    async function handleMouseOver(e) {
        const el = e.target && e.target.closest ? e.target.closest("[heimdall-content-hover]") : null;
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

        const prev = _hoverTimers.get(el);
        if (prev) clearTimeout(prev);

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

    async function handleMouseOut(e) {
        const el = e.target && e.target.closest ? e.target.closest("[heimdall-content-hover]") : null;
        if (!el)
            return;

        const to = e.relatedTarget;
        if (to && (to === el || (el.contains && el.contains(to))))
            return;

        const tid = _hoverTimers.get(el);
        if (tid) {
            clearTimeout(tid);
            _hoverTimers.delete(el);
        }
    }

    // ============================================================
    // DOM observer
    // ============================================================

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

    // ============================================================
    // Public API
    // ============================================================

    const Heimdall = {
        version: VERSION,
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

            visibleRootMargin: "0px",
            visibleThreshold: 0,

            oobEnabled: true,
            oobAllowedTargets: null,

            sseDefaultSwap: "none",
            sseEventName: "heimdall",
            sseSweepIntervalMs: 5000,
            ssePauseWhenHidden: false
        }
    };

    global.Heimdall = Heimdall;

    // ============================================================
    // Startup
    // ============================================================

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
