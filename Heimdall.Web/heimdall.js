(function (global) {
    "use strict";

    const DEFAULT_ENDPOINT = "/__Heimdall/content/actions";
    const CSRF_ENDPOINT = "/__Heimdall/csrf";
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
        }
        else {
            fn();
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
                if (!Array.isArray(obj[k]))
                    obj[k] = [obj[k]];

                obj[k].push(v);
            } else {
                obj[k] = v;
            }
        }
        return obj;
    }

    function payloadFromElement(el) {
        const payloadAttr = getAttr(el, "heimdall-payload");
        if (payloadAttr)
            return safeJsonParse(payloadAttr);

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

        const form = document.querySelector(from);
        if (form && form.tagName === "FORM") {
            return formDataToObject(new FormData(form));
        }

        return null;
    }

    let csrfToken = null;
    let csrfTokenPromise = null;

    async function ensureCsrfToken() {
        if (csrfToken)
            return csrfToken;

        if (csrfTokenPromise)
            return csrfTokenPromise;

        csrfTokenPromise = (async () => {
            try {
                const res = await fetch(CSRF_ENDPOINT, {
                    method: "GET",
                    credentials: "same-origin",
                    headers: {
                        "X-Requested-With": "XMLHttpRequest"
                    }
                });

                if (!res.ok) {
                    throw new Error(`CSRF token fetch failed: ${res.status}`);
                }

                const data = await res.json();
                csrfToken = data.requestToken;
                return csrfToken;
            }
            finally {
                csrfTokenPromise = null;
            }
        })();

        return csrfTokenPromise;
    }

    function clearCsrfToken() {
        csrfToken = null;
        csrfTokenPromise = null;
    }

    async function invoke(actionId, payload, options) {
        return _invokeWithRetry(actionId, payload, options, true);
    }

    async function _invokeWithRetry(actionId, payload, options, shouldRetry) {
        options = options || {};

        const endpoint = options.endpoint || Waaagh.config.endpoint;
        const targetEl = resolveTarget(options.target, options.fallbackTarget || null);
        const swap = options.swap || "inner";
        const credentials = options.credentials || "same-origin";

        // Get CSRF token
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

        const body = payload == null ? "{}" : JSON.stringify(payload);

        const res = await fetch(endpoint, {
            method: "POST",
            headers,
            body,
            credentials
        });

        // Handle CSRF token expiration/validation failure
        if (res.status === 400 && shouldRetry) {
            const text = await safeText(res);
            // Check if this looks like a CSRF validation error
            if (text && (text.toLowerCase().includes("antiforgery") ||
                text.toLowerCase().includes("csrf") ||
                text.toLowerCase().includes("validation"))) {
                clearCsrfToken();
                // Retry once with a fresh token
                return _invokeWithRetry(actionId, payload, options, false);
            }
        }

        if (!res.ok) {
            const text = await safeText(res);
            const err = new Error(`Heimdall failed (${res.status}): ${text || res.statusText}`);
            err.status = res.status;
            err.body = text;
            throw err;
        }

        const html = await res.text();

        if (targetEl) setHtml(targetEl, html, swap);

        if (typeof options.onSuccess === "function") {
            options.onSuccess({ html, target: targetEl, response: res });
        }

        return html;
    }

    function bootLoads() {
        const nodes = document.querySelectorAll("[heimdall-content-load]");
        for (const el of nodes) {
            if (el.__waaaghLoaded)
                continue;

            el.__waaaghLoaded = true;

            const actionId = getAttr(el, "heimdall-content-load");
            if (!actionId)
                continue;

            const target = getAttr(el, "heimdall-content-target") || el;
            const swap = getAttr(el, "heimdall-content-swap") || "inner";
            const payload = payloadFromElement(el);

            invoke(actionId, payload, { target, swap, fallbackTarget: el }).catch(err => console.error(err));
        }
    }

    async function handleClick(e) {
        const el = e.target && e.target.closest ? e.target.closest("[heimdall-click]") : null;
        if (!el)
            return;

        if (e.defaultPrevented)
            return;

        if (e.button != null && e.button !== 0)
            return;
        if (e.metaKey || e.ctrlKey || e.shiftKey || e.altKey)
            return;

        if (el.hasAttribute("disabled") || el.getAttribute("aria-disabled") === "true")
            return;

        const actionId = getAttr(el, "heimdall-click");
        if (!actionId)
            return;

        const isAnchor = el.tagName === "A";
        const preventDefault = truthyAttr(el, "heimdall-prevent-default", isAnchor);
        if (preventDefault)
            e.preventDefault();

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
        }
        catch (err) {
            console.error(err);
        }
        finally {
            if (shouldDisable) {
                el.removeAttribute("aria-busy");
                if (!wasDisabled) el.removeAttribute("disabled");
            }
        }
    }

    function boot() {
        bootLoads();
    }

    const Waaagh = {
        invoke,
        boot,
        onReady,
        clearCsrfToken,
        config: {
            endpoint: DEFAULT_ENDPOINT
        }
    };

    global.Waaagh = Waaagh;

    onReady(() => {
        if (!document.__waaaghClickInstalled) {
            document.__waaaghClickInstalled = true;
            document.addEventListener("click", handleClick, true);
        }

        boot();

        if (global.Blazor && typeof global.Blazor.addEventListener === "function") {
            global.Blazor.addEventListener("enhancedload", () => {
                boot();
            });
        }
    });

})(window);