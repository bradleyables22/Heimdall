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

    const _bifrostTokenByTopic = new Map();
    const _bifrostTokenPromiseByTopic = new Map();

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

