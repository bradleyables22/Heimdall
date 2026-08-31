const MEDIA_QUERIES = [
    "(prefers-color-scheme: dark)",
    "(prefers-color-scheme: light)",
    "(prefers-reduced-motion: reduce)",
    "(prefers-contrast: more)",
    "(prefers-contrast: less)",
    "(prefers-contrast: custom)",
    "(forced-colors: active)",
    "(pointer: coarse)",
    "(pointer: fine)",
    "(hover: hover)"
];

export function createClientInfoRuntime({ global, getConfig, emitLifecycle, dbg }) {
    const media = new Map();
    let installed = false;
    let dirty = true;
    let cachedInfo = null;
    let capturedAt = 0;

    function markDirty() {
        dirty = true;
    }

    function getMediaQuery(query) {
        if (media.has(query))
            return media.get(query);

        let result = null;
        try {
            result = typeof global.matchMedia === "function"
                ? global.matchMedia(query)
                : null;
        } catch {
            result = null;
        }

        media.set(query, result);
        return result;
    }

    function matches(query) {
        return getMediaQuery(query)?.matches === true;
    }

    function listen(target, eventName) {
        if (target && typeof target.addEventListener === "function")
            target.addEventListener(eventName, markDirty, { passive: true });
    }

    function installInvalidationListeners() {
        if (installed)
            return;
        installed = true;

        for (const eventName of ["resize", "orientationchange", "languagechange", "online", "offline"])
            listen(global, eventName);

        listen(global.visualViewport, "resize");
        listen(global.screen?.orientation, "change");

        for (const query of MEDIA_QUERIES) {
            const result = getMediaQuery(query);
            if (!result)
                continue;
            if (typeof result.addEventListener === "function")
                result.addEventListener("change", markDirty);
            else if (typeof result.addListener === "function")
                result.addListener(markDirty);
        }
    }

    function finiteNumber(value, fallback = 0) {
        const number = Number(value);
        return Number.isFinite(number) ? number : fallback;
    }

    function wholeNumber(value, fallback = 0) {
        return Math.max(0, Math.round(finiteNumber(value, fallback)));
    }

    function shortString(value, maxLength) {
        return String(value || "").trim().slice(0, maxLength);
    }

    function preferredContrast() {
        if (matches("(prefers-contrast: more)")) return "more";
        if (matches("(prefers-contrast: less)")) return "less";
        if (matches("(prefers-contrast: custom)")) return "custom";
        return "no-preference";
    }

    function colorScheme() {
        if (matches("(prefers-color-scheme: dark)")) return "dark";
        if (matches("(prefers-color-scheme: light)")) return "light";
        return "no-preference";
    }

    function collect() {
        const navigator = global.navigator || {};
        const screen = global.screen || {};
        const viewport = global.visualViewport;
        const viewportWidth = finiteNumber(viewport?.width, finiteNumber(global.innerWidth));
        const viewportHeight = finiteNumber(viewport?.height, finiteNumber(global.innerHeight));
        const screenWidth = wholeNumber(screen.width, viewportWidth);
        const screenHeight = wholeNumber(screen.height, viewportHeight);
        const maxTouchPoints = wholeNumber(navigator.maxTouchPoints);
        const touch = maxTouchPoints > 0 || "ontouchstart" in global;
        const coarsePointer = matches("(pointer: coarse)");
        const pointer = coarsePointer
            ? "coarse"
            : (matches("(pointer: fine)") ? "fine" : "none");
        const shortSide = Math.min(
            screenWidth || wholeNumber(viewportWidth),
            screenHeight || wholeNumber(viewportHeight));
        const deviceCategory = touch && coarsePointer
            ? (shortSide < 600 ? "mobile" : (shortSide < 1024 ? "tablet" : "desktop"))
            : "desktop";

        let timeZone = "";
        try {
            timeZone = shortString(global.Intl?.DateTimeFormat().resolvedOptions().timeZone, 128);
        } catch {
            timeZone = "";
        }

        const languages = Array.isArray(navigator.languages)
            ? navigator.languages
            : [navigator.language];

        return {
            timeZone: timeZone || null,
            utcOffsetMinutes: -new Date().getTimezoneOffset(),
            locale: shortString(navigator.language, 64) || null,
            languages: languages
                .map(language => shortString(language, 64))
                .filter(Boolean)
                .slice(0, 16),
            viewportWidth,
            viewportHeight,
            screenWidth,
            screenHeight,
            devicePixelRatio: finiteNumber(global.devicePixelRatio, 1),
            orientation: viewportHeight >= viewportWidth ? "portrait" : "landscape",
            deviceCategory,
            colorScheme: colorScheme(),
            prefersReducedMotion: matches("(prefers-reduced-motion: reduce)"),
            prefersContrast: preferredContrast(),
            forcedColors: matches("(forced-colors: active)"),
            touch,
            maxTouchPoints,
            pointer,
            hover: matches("(hover: hover)"),
            online: navigator.onLine !== false
        };
    }

    function cloneInfo(info) {
        return {
            ...info,
            languages: Array.isArray(info.languages) ? [...info.languages] : []
        };
    }

    function getHeaderValue(requestContext) {
        const config = getConfig();
        if (!config || config.clientInfo !== true)
            return null;

        installInvalidationListeners();

        const configuredMaxAge = Number(config.clientInfoMaxAgeMs);
        const maxAge = Number.isFinite(configuredMaxAge)
            ? Math.max(0, configuredMaxAge)
            : 60000;
        const now = Date.now();
        const age = now - capturedAt;

        if (!cachedInfo || dirty || age < 0 || age >= maxAge) {
            try {
                cachedInfo = collect();
                capturedAt = now;
                dirty = false;
            } catch (error) {
                cachedInfo = null;
                if (typeof dbg === "function")
                    dbg("client information collection failed", error);
            }
        }

        if (!cachedInfo)
            return null;

        const detail = {
            actionId: requestContext?.actionId || null,
            requestId: requestContext?.requestId || null,
            attempt: requestContext?.attempt || 0,
            sourceElement: requestContext?.sourceElement || null,
            info: cloneInfo(cachedInfo)
        };
        const shouldInclude = typeof emitLifecycle !== "function" || emitLifecycle(
            requestContext?.sourceElement || null,
            "heimdall:client-info-before",
            detail,
            { cancelable: true }
        );

        if (!shouldInclude || detail.info == null)
            return null;

        try {
            return JSON.stringify(detail.info);
        } catch (error) {
            if (typeof dbg === "function")
                dbg("client information serialization failed", error);
            return null;
        }
    }

    return {
        getHeaderValue,
        invalidate: markDirty
    };
}
