import {
    getAuthRedirectUrlFromResponse,
    normalizeFollowedAuthRedirectUrl
} from "./auth-redirects.js";
import { emitUnauthorized } from "./unauthorized.js";

export function createSecurityTokens({
    global,
    getConfig,
    emit,
    emitLifecycle,
    dbg,
    safeText,
    resolveRequestHeaders,
    csrfHeader,
    defaultBifrostTokenEndpoint
}) {
    let csrfToken = null;
    let csrfTokenPromise = null;

    const _bifrostTokenByTopic = new Map();
    const _bifrostTokenPromiseByTopic = new Map();

    async function ensureCsrfToken() {
        if (csrfToken)
            return csrfToken;
        if (csrfTokenPromise)
            return csrfTokenPromise;

        csrfTokenPromise = (async () => {
            try {
                const configuredUrl = getConfig().endpoints.csrf;
                const url = new URL(configuredUrl, global.location?.origin || undefined).toString();
                let headers = { "X-Requested-With": "XMLHttpRequest" };
                if (typeof resolveRequestHeaders === "function") {
                    headers = await resolveRequestHeaders({
                        kind: "csrf-token",
                        url,
                        method: "GET",
                        actionId: null,
                        topic: null,
                        requestId: null,
                        attempt: 1,
                        sourceElement: null,
                        signal: null
                    }, headers);
                }

                const res = await global.fetch(url, {
                    method: "GET",
                    credentials: "same-origin",
                    headers
                });

                if (!res.ok) {
                    const body = await safeText(res);
                    const authRedirectUrl = getAuthRedirectUrlFromResponse(res);
                    const redirectUrl = authRedirectUrl
                        ? normalizeFollowedAuthRedirectUrl(global, getConfig, authRedirectUrl)
                        : null;
                    const useDefaultUnauthorizedHandling = emitUnauthorized({
                        response: res,
                        emitLifecycle,
                        sourceElement: null,
                        detail: {
                            kind: "csrf-token",
                            actionId: null,
                            topic: null,
                            requestId: null,
                            attempt: 1,
                            sourceElement: null,
                            url,
                            method: "GET",
                            body,
                            redirectUrl,
                            requestContext: null
                        }
                    });

                    const performedRedirect = !!(authRedirectUrl && useDefaultUnauthorizedHandling);
                    if (performedRedirect) {
                        if (typeof dbg === "function")
                            dbg("csrf token redirecting", { redirectUrl });
                        global.location.href = redirectUrl;
                    }

                    const error = new Error(`CSRF token fetch failed: ${res.status}. ${body || ""}`.trim());
                    error.status = res.status;
                    error.body = body;
                    error.redirectUrl = performedRedirect ? redirectUrl : null;
                    throw error;
                }

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

    function clearBifrostSubscribeToken(topic) {
        const t = String(topic || "").trim();

        if (!t) {
            _bifrostTokenByTopic.clear();
            _bifrostTokenPromiseByTopic.clear();
            return;
        }

        _bifrostTokenByTopic.delete(t);
        _bifrostTokenPromiseByTopic.delete(t);
    }

    function isAntiforgeryFailure(status, body) {
        if (Number(status) !== 400)
            return false;

        const lower = String(body || "").toLowerCase();
        return lower.includes("csrf") || lower.includes("antiforgery");
    }

    async function fetchBifrostSubscribeToken(t, shouldRetry) {
        const config = getConfig();
        const antiforgeryEnabled = config.antiforgery !== false;
        const csrf = antiforgeryEnabled ? await ensureCsrfToken() : null;

        const base = config.endpoints && config.endpoints.bifrostToken
            ? config.endpoints.bifrostToken
            : defaultBifrostTokenEndpoint;

        const url = new URL(base, global.location?.origin || undefined);
        url.searchParams.set("topic", t);

        let headers = {
            "X-Requested-With": "XMLHttpRequest"
        };
        if (antiforgeryEnabled)
            headers[csrfHeader] = csrf;

        if (typeof resolveRequestHeaders === "function") {
            headers = await resolveRequestHeaders({
                kind: "bifrost-token",
                url: url.toString(),
                method: "GET",
                actionId: null,
                topic: t,
                requestId: null,
                attempt: shouldRetry ? 1 : 2,
                sourceElement: null,
                signal: null
            }, headers);
        }

        const res = await global.fetch(url.toString(), {
            method: "GET",
            credentials: "same-origin",
            headers
        });

        let responseBody = null;
        if (res.status === 401)
            responseBody = await safeText(res);

        const authRedirectUrl = getAuthRedirectUrlFromResponse(res);
        const normalizedAuthRedirectUrl = authRedirectUrl
            ? normalizeFollowedAuthRedirectUrl(global, getConfig, authRedirectUrl)
            : null;
        const useDefaultUnauthorizedHandling = emitUnauthorized({
            response: res,
            emitLifecycle,
            sourceElement: null,
            detail: {
                kind: "bifrost-token",
                actionId: null,
                topic: t,
                requestId: null,
                attempt: shouldRetry ? 1 : 2,
                sourceElement: null,
                url: url.toString(),
                method: "GET",
                body: responseBody,
                redirectUrl: normalizedAuthRedirectUrl,
                requestContext: null
            }
        });
        if (authRedirectUrl && useDefaultUnauthorizedHandling) {
            const redirectUrl = normalizedAuthRedirectUrl;
            const error = new Error(`Bifrost token fetch redirected: ${redirectUrl}`);
            error.status = res.status;
            error.redirectUrl = redirectUrl;

            if (typeof emit === "function") {
                emit("heimdall:sse-redirect", {
                    topic: t,
                    url: url.toString(),
                    status: res.status,
                    redirectUrl
                });
            }

            if (typeof dbg === "function")
                dbg("sse token redirecting", { topic: t, redirectUrl });

            global.location.href = redirectUrl;
            throw error;
        }

        if (!res.ok) {
            const body = responseBody == null ? await safeText(res) : responseBody;

            if (antiforgeryEnabled && shouldRetry && isAntiforgeryFailure(res.status, body)) {
                if (typeof dbg === "function")
                    dbg("bifrost csrf validation suspected; retrying once with fresh token", { topic: t });

                clearCsrfToken();
                return fetchBifrostSubscribeToken(t, false);
            }

            const error = new Error(`Bifrost token fetch failed: ${res.status}. ${body || ""}`.trim());
            error.status = res.status;
            error.body = body;
            throw error;
        }

        const data = await res.json();
        const token = data && (data.token || data.st);
        const expiresInSeconds = data && (data.expiresInSeconds || data.expires_in_seconds || 120);

        if (!token)
            throw new Error("Bifrost token response missing token.");

        const ttlMs = Math.max(5, parseInt(expiresInSeconds, 10) || 120) * 1000;
        const expiresAtMs = Date.now() + Math.max(5000, ttlMs - 5000);

        _bifrostTokenByTopic.set(t, { token, expiresAtMs });
        return token;
    }

    async function ensureBifrostSubscribeToken(topic) {
        const t = String(topic || "").trim();
        if (!t)
            throw new Error("Bifrost topic is required.");

        const cached = _bifrostTokenByTopic.get(t);
        if (cached && cached.token && cached.expiresAtMs && Date.now() < cached.expiresAtMs) {
            return cached.token;
        }

        const inflight = _bifrostTokenPromiseByTopic.get(t);
        if (inflight)
            return inflight;

        const p = (async () => {
            try {
                return await fetchBifrostSubscribeToken(t, true);
            } finally {
                _bifrostTokenPromiseByTopic.delete(t);
            }
        })();

        _bifrostTokenPromiseByTopic.set(t, p);
        return p;
    }

    return {
        clearBifrostSubscribeToken,
        clearCsrfToken,
        ensureBifrostSubscribeToken,
        ensureCsrfToken
    };
}
