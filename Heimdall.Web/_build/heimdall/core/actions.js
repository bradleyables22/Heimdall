import {
    formDataToObject,
    getAttr,
    resolveTarget,
    safeText,
    truthyAttr
} from "./utils.js";

export function createActionInvoker({
    global,
    getConfig,
    ensureCsrfToken,
    clearCsrfToken,
    emit,
    dbg,
    payloadFromElement,
    boot,
    dom,
    actionHeader,
    csrfHeader
}) {
    async function invoke(actionId, payload, options) {
        return _invokeWithRetry(actionId, payload, options, true);
    }

    async function _invokeWithRetry(actionId, payload, options, shouldRetry) {
        options = options || {};

        const config = getConfig();
        const endpointBase = options.endpoint || config.endpoints.contentActions;
        const targetEl = resolveTarget(options.target, options.fallbackTarget || null);
        const swap = options.swap || "inner";

        const url = new URL(endpointBase, global.location?.origin || undefined);
        url.searchParams.set("action", actionId);

        const token = await ensureCsrfToken();

        const headers = {
            "Content-Type": "application/json",
            [actionHeader]: actionId,
            [csrfHeader]: token
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
            res = await global.fetch(url.toString(), {
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

        // FIX: buffer the response body once so we can both inspect it for CSRF
        // errors AND use it in the error path, without double-consuming the stream.
        const rawHtml = await safeText(res);
        const ms = performance.now() - started;

        if (res.status === 400 && shouldRetry) {
            const lower = rawHtml.toLowerCase();
            if (lower.includes("csrf") || lower.includes("antiforgery")) {
                dbg("csrf validation suspected; retrying once with fresh token");
                clearCsrfToken();
                return _invokeWithRetry(actionId, payload, options, false);
            }
        }

        let html = rawHtml;
        let abortSwap = false;
        let abortReason = null;
        let redirectUrl = null;

        if (res.ok) {
            const oob = dom.processOob(html, options && options.sourceEl ? options.sourceEl : null);
            html = oob.html;
            abortSwap = !!oob.abortSwap;
            abortReason = oob.abortReason || null;
            redirectUrl = oob.redirectUrl || null;
        } else {
            html = dom.sanitizeHtmlStringNoApply(html);
        }

        if (res.ok && redirectUrl) {
            emit("heimdall:redirect", {
                actionId,
                payload,
                target: targetEl,
                swap,
                endpoint: url.toString(),
                status: res.status,
                url: redirectUrl
            });

            dbg("redirecting", { actionId, url: redirectUrl });
            global.location.href = redirectUrl;

            return {
                ok: true,
                status: res.status,
                html: null,
                error: null,
                response: res,
                ms,
                abortSwap: true,
                abortReason: "redirect",
                redirectUrl
            };
        }

        if (res.ok && abortSwap) {
            emit("heimdall:abort", { actionId, payload, target: targetEl, swap, endpoint: url.toString(), status: res.status, reason: abortReason });
            dbg("swap aborted", { actionId, reason: abortReason, target: targetEl });
        }

        if (res.ok && targetEl && !abortSwap) {
            const mainTpl = dom.parseHtmlToTemplate(html);
            dom.stripInvocationsFromFragment(mainTpl.content);
            dom.stripAbortsFromFragment(mainTpl.content);
            dom.stripRedirectsFromFragment(mainTpl.content);

            const { didApply, appliedRoot } = dom.applySwap(targetEl, mainTpl.content, swap);

            if (didApply && !getConfig().observeDom) {
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
            ms,
            abortSwap,
            abortReason,
            redirectUrl
        };

        if (!res.ok) {
            emit("heimdall:error", { actionId, payload, target: targetEl, swap, status: res.status, body: html });
        } else {
            emit("heimdall:after", { actionId, payload, target: targetEl, swap, endpoint: url.toString(), status: res.status, ms, html, redirectUrl });
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

    return {
        invoke,
        runActionFromElement
    };
}
