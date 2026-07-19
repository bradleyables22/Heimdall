import {
    formDataToObject,
    getAttr,
    resolveTarget,
    safeText,
    truthyAttr
} from "./utils.js";
import {
    getAuthRedirectUrlFromResponse,
    normalizeFollowedAuthRedirectUrl
} from "./auth-redirects.js";

export function createActionInvoker({
    global,
    getConfig,
    ensureCsrfToken,
    clearCsrfToken,
    emit,
    emitLifecycle,
    dbg,
    payloadFromElement,
    boot,
    dom,
    coordinator,
    actionHeader,
    csrfHeader
}) {
    const busyStates = new WeakMap();
    let nextRequestId = 0;

    function acquireBusyState(el) {
        if (!el)
            return;

        let state = busyStates.get(el);
        if (!state) {
            state = {
                count: 0,
                hadDisabled: false,
                hadAriaBusy: false,
                ariaBusy: null
            };
            busyStates.set(el, state);
        }

        if (state.count === 0) {
            state.hadDisabled = el.hasAttribute("disabled");
            state.hadAriaBusy = el.hasAttribute("aria-busy");
            state.ariaBusy = el.getAttribute("aria-busy");
        }

        state.count++;
        el.setAttribute("disabled", "disabled");
        el.setAttribute("aria-busy", "true");
    }

    function releaseBusyState(el) {
        if (!el)
            return;

        const state = busyStates.get(el);
        if (!state)
            return;

        state.count = Math.max(0, state.count - 1);
        if (state.count > 0)
            return;

        if (!state.hadDisabled)
            el.removeAttribute("disabled");

        if (state.hadAriaBusy)
            el.setAttribute("aria-busy", state.ariaBusy == null ? "" : state.ariaBusy);
        else
            el.removeAttribute("aria-busy");

        busyStates.delete(el);
    }

    function createRequestContext(actionId, payload, options) {
        const config = getConfig();
        const sourceElement = options.sourceEl || null;
        const configuredSync = options.sync != null
            ? options.sync
            : (sourceElement ? getAttr(sourceElement, "heimdall-sync") : null);
        const configuredGroup = options.syncGroup != null
            ? options.syncGroup
            : (sourceElement ? getAttr(sourceElement, "heimdall-sync-group") : null);

        return {
            requestId: ++nextRequestId,
            attempt: 0,
            actionId,
            trigger: options.triggerName || null,
            sourceElement,
            payload,
            target: options.target,
            fallbackTarget: options.fallbackTarget || null,
            swap: options.swap || "inner",
            endpoint: options.endpoint || config.endpoints.contentActions,
            headers: Object.assign({}, options.headers || {}),
            sync: configuredSync || config.requestSync || "parallel",
            syncGroup: configuredGroup && String(configuredGroup).trim()
                ? String(configuredGroup).trim()
                : null,
            timeoutMs: options.timeoutMs != null
                ? Number(options.timeoutMs)
                : Number(config.requestTimeoutMs || 0),
            externalSignal: options.signal || null,
            disableElement: options.disableElement || null,
            controller: null,
            signal: null,
            request: null,
            response: null,
            rawHtml: null,
            result: null,
            cancelled: false,
            cancelReason: null,
            timedOut: false,
            startedAt: performance.now(),
            startedExecutionAt: null,
            finishedAt: null
        };
    }

    function emitCancellation(context, result) {
        const detail = context;
        detail.result = result;

        if (result.cancelReason === "timeout")
            emitLifecycle(context.sourceElement, "heimdall:request-timeout", detail);

        emitLifecycle(context.sourceElement, "heimdall:request-cancel", detail);
    }

    async function invoke(actionId, payload, options) {
        options = options || {};
        const context = createRequestContext(actionId, payload, options);

        emitLifecycle(context.sourceElement, "heimdall:request-config", context);
        context.target = resolveTarget(context.target, context.fallbackTarget);
        context.sync = coordinator.normalizeStrategy(context.sync, getConfig().requestSync || "parallel");

        try {
            const result = await coordinator.run(context, async () => {
                acquireBusyState(context.disableElement);
                try {
                    return await executeRequestAttempt(context, options, true);
                } finally {
                    releaseBusyState(context.disableElement);
                }
            });

            context.result = result;

            if (result && result.cancelled) {
                emitCancellation(context, result);
            } else if (context.response) {
                emitLifecycle(context.sourceElement, "heimdall:request-after", context);
            }

            return result;
        } finally {
            context.finishedAt = performance.now();
            emitLifecycle(context.sourceElement, "heimdall:request-finally", context);
        }
    }

    async function executeRequestAttempt(context, options, shouldRetry) {
        context.attempt++;

        const url = new URL(context.endpoint, global.location?.origin || undefined);
        url.searchParams.set("action", context.actionId);

        const token = await ensureCsrfToken();
        if (!coordinator.isCurrent(context))
            return coordinator.getCancellationResult(context);

        const headers = {
            "Content-Type": "application/json",
            [actionHeader]: context.actionId,
            [csrfHeader]: token
        };

        for (const key in context.headers)
            headers[key] = context.headers[key];

        let body = "{}";
        try {
            body = context.payload == null ? "{}" : JSON.stringify(context.payload);
        } catch (e) {
            const err = new Error(`Heimdall payload is not JSON-serializable for action '${context.actionId}'.`);
            err.cause = e;
            emit("heimdall:error", {
                actionId: context.actionId,
                payload: context.payload,
                target: context.target,
                swap: context.swap,
                status: 0,
                error: err
            });
            throw err;
        }

        context.request = {
            url: url.toString(),
            headers,
            body,
            credentials: "same-origin",
            signal: context.signal
        };

        const shouldContinue = emitLifecycle(
            context.sourceElement,
            "heimdall:request-before",
            context,
            { cancelable: true }
        );

        if (!shouldContinue) {
            coordinator.cancel(context, "event-cancelled");
            return coordinator.getCancellationResult(context);
        }

        if (!coordinator.isCurrent(context))
            return coordinator.getCancellationResult(context);

        const attemptStarted = performance.now();
        emit("heimdall:before", {
            actionId: context.actionId,
            payload: context.payload,
            target: context.target,
            swap: context.swap,
            endpoint: context.request.url
        });

        dbg("invoke ->", context.actionId, {
            endpoint: context.request.url,
            swap: context.swap,
            target: context.target,
            requestId: context.requestId,
            attempt: context.attempt
        });

        let response;
        try {
            response = await global.fetch(context.request.url, {
                method: "POST",
                headers: context.request.headers,
                body: context.request.body,
                credentials: context.request.credentials,
                signal: context.request.signal || undefined
            });
        } catch (networkError) {
            if (context.cancelled || (networkError && networkError.name === "AbortError" && context.signal && context.signal.aborted))
                return coordinator.getCancellationResult(context);

            const result = {
                ok: false,
                status: 0,
                error: networkError.message,
                response: null,
                html: null,
                ms: performance.now() - attemptStarted,
                requestId: context.requestId
            };
            emit("heimdall:error", {
                actionId: context.actionId,
                payload: context.payload,
                target: context.target,
                swap: context.swap,
                error: networkError
            });
            return result;
        }

        const rawHtml = await safeText(response);
        const ms = performance.now() - attemptStarted;
        context.response = response;
        context.rawHtml = rawHtml;

        if (!coordinator.isCurrent(context))
            return coordinator.getCancellationResult(context);

        if (response.status === 400 && shouldRetry) {
            const lower = rawHtml.toLowerCase();
            if (lower.includes("csrf") || lower.includes("antiforgery")) {
                dbg("csrf validation suspected; retrying once with fresh token");
                clearCsrfToken();
                return executeRequestAttempt(context, options, false);
            }
        }

        const authRedirectUrl = getAuthRedirectUrlFromResponse(response);
        if (authRedirectUrl) {
            const redirectUrl = normalizeFollowedAuthRedirectUrl(global, getConfig, authRedirectUrl);

            emit("heimdall:redirect", {
                actionId: context.actionId,
                payload: context.payload,
                target: context.target,
                swap: context.swap,
                endpoint: context.request.url,
                status: response.status,
                url: redirectUrl
            });

            dbg("redirecting", { actionId: context.actionId, url: redirectUrl });
            global.location.href = redirectUrl;

            return {
                ok: response.ok,
                status: response.status,
                html: null,
                error: null,
                response,
                ms,
                abortSwap: true,
                abortReason: "redirect",
                redirectUrl,
                requestId: context.requestId
            };
        }

        let html = rawHtml;
        let abortSwap = false;
        let abortReason = null;
        let redirectUrl = null;
        let jsAfter = [];

        if (response.ok) {
            const oob = dom.processOob(html, context.sourceElement, {
                kind: "action",
                actionId: context.actionId,
                payload: context.payload,
                target: context.target,
                swap: context.swap,
                endpoint: context.request.url,
                status: response.status,
                sourceEl: context.sourceElement,
                requestContext: context
            });
            html = oob.html;
            abortSwap = !!oob.abortSwap;
            abortReason = oob.abortReason || null;
            redirectUrl = oob.redirectUrl || null;
            jsAfter = oob.jsAfter || [];
        } else {
            html = dom.sanitizeHtmlStringNoApply(html);
        }

        if (response.ok && redirectUrl) {
            emit("heimdall:redirect", {
                actionId: context.actionId,
                payload: context.payload,
                target: context.target,
                swap: context.swap,
                endpoint: context.request.url,
                status: response.status,
                url: redirectUrl
            });

            dbg("redirecting", { actionId: context.actionId, url: redirectUrl });
            global.location.href = redirectUrl;

            return {
                ok: true,
                status: response.status,
                html: null,
                error: null,
                response,
                ms,
                abortSwap: true,
                abortReason: "redirect",
                redirectUrl,
                requestId: context.requestId
            };
        }

        if (response.ok && abortSwap) {
            emit("heimdall:abort", {
                actionId: context.actionId,
                payload: context.payload,
                target: context.target,
                swap: context.swap,
                endpoint: context.request.url,
                status: response.status,
                reason: abortReason
            });
            dbg("swap aborted", { actionId: context.actionId, reason: abortReason, target: context.target });
        }

        if (response.ok && context.target && !abortSwap) {
            const mainTemplate = dom.parseHtmlToTemplate(html);
            dom.stripInvocationsFromFragment(mainTemplate.content);
            dom.stripAbortsFromFragment(mainTemplate.content);
            dom.stripRedirectsFromFragment(mainTemplate.content);
            dom.stripJsInvokeVoidFromFragment(mainTemplate.content);

            const swapResult = dom.applySwap(
                context.target,
                mainTemplate.content,
                context.swap,
                {
                    kind: "action",
                    swapKind: "main",
                    sourceEl: context.sourceElement,
                    requestContext: context
                }
            );
            const { didApply, appliedRoot } = swapResult;

            if (didApply && !getConfig().observeDom) {
                try {
                    boot(appliedRoot || swapResult.target || context.target);
                } catch {
                    // ignore
                }
            }
        }

        if (response.ok) {
            dom.invokeJsInvokeVoidDirectives(jsAfter, {
                phase: "after",
                kind: "action",
                actionId: context.actionId,
                payload: context.payload,
                target: context.target,
                swap: context.swap,
                endpoint: context.request.url,
                status: response.status
            });
        }

        const result = {
            ok: response.ok,
            status: response.status,
            html: response.ok ? html : null,
            error: response.ok ? null : html,
            response,
            ms,
            abortSwap,
            abortReason,
            redirectUrl,
            requestId: context.requestId
        };

        if (!response.ok) {
            emit("heimdall:error", {
                actionId: context.actionId,
                payload: context.payload,
                target: context.target,
                swap: context.swap,
                status: response.status,
                body: html
            });
        } else {
            emit("heimdall:after", {
                actionId: context.actionId,
                payload: context.payload,
                target: context.target,
                swap: context.swap,
                endpoint: context.request.url,
                status: response.status,
                ms,
                html,
                redirectUrl
            });
        }

        if (typeof options.onSuccess === "function" && response.ok)
            options.onSuccess(result);
        if (typeof options.onError === "function" && !response.ok)
            options.onError(result);

        dbg("invoke <-", context.actionId, result);
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
        const sync = getAttr(el, "heimdall-sync");
        const syncGroup = getAttr(el, "heimdall-sync-group");

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

        return { target, swap, payload, sync, syncGroup };
    }

    async function runActionFromElement(el, actionId, triggerName, extraOptions) {
        if (!el || !actionId)
            return;
        if (el.hasAttribute("disabled") || el.getAttribute("aria-disabled") === "true")
            return;

        const { target, swap, payload, sync, syncGroup } = getCommonOptions(el, triggerName);

        const defaultDisable = DEFAULT_DISABLE_BY_TRIGGER[triggerName] ?? false;
        const shouldDisable = truthyAttr(el, "heimdall-content-disable", defaultDisable);
        const opts = Object.assign({
            target,
            swap,
            sync,
            syncGroup,
            fallbackTarget: el,
            sourceEl: el,
            triggerName,
            disableElement: shouldDisable ? el : null
        }, extraOptions || {});

        try {
            await invoke(actionId, payload, opts);
        } catch (err) {
            // eslint-disable-next-line no-console
            console.error(err);
        }
    }

    return {
        invoke,
        runActionFromElement
    };
}
