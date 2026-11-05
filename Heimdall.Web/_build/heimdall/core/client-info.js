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

const DEFAULT_LIMITS = {
    maxKeysPerArea: 16,
    maxKeyLength: 128,
    maxValueLength: 1024,
    maxStorageChars: 2048,
    maxExtensionEntries: 16,
    maxExtensionChars: 1024,
    maxExtensionDepth: 4
};

const MAX_HEADER_LENGTH = 4096;

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

    function nonNegativeLimit(value, fallback) {
        const number = Number(value);
        return Number.isFinite(number)
            ? Math.max(0, Math.floor(number))
            : fallback;
    }

    function normalizeLimits(config) {
        const configured = config?.clientInfoLimits;
        return {
            maxKeysPerArea: nonNegativeLimit(configured?.maxKeysPerArea, DEFAULT_LIMITS.maxKeysPerArea),
            maxKeyLength: nonNegativeLimit(configured?.maxKeyLength, DEFAULT_LIMITS.maxKeyLength),
            maxValueLength: nonNegativeLimit(configured?.maxValueLength, DEFAULT_LIMITS.maxValueLength),
            maxStorageChars: nonNegativeLimit(configured?.maxStorageChars, DEFAULT_LIMITS.maxStorageChars),
            maxExtensionEntries: nonNegativeLimit(configured?.maxExtensionEntries, DEFAULT_LIMITS.maxExtensionEntries),
            maxExtensionChars: nonNegativeLimit(configured?.maxExtensionChars, DEFAULT_LIMITS.maxExtensionChars),
            maxExtensionDepth: nonNegativeLimit(configured?.maxExtensionDepth, DEFAULT_LIMITS.maxExtensionDepth)
        };
    }

    function normalizeClientInfoConfig(config) {
        const raw = config?.clientInfo;
        const enabled = raw === true
            ? true
            : !!(raw && typeof raw === "object" && raw.enabled === true);
        const storage = raw && typeof raw === "object" && raw.storage && typeof raw.storage === "object"
            ? raw.storage
            : {};

        function keysFor(area) {
            if (!Array.isArray(storage[area]))
                return [];

            const seen = new Set();
            const result = [];
            for (const rawKey of storage[area]) {
                const key = String(rawKey ?? "").trim();
                if (!key || seen.has(key))
                    continue;
                seen.add(key);
                result.push(key);
            }
            return result;
        }

        return {
            enabled,
            local: keysFor("local"),
            session: keysFor("session"),
            limits: normalizeLimits(config)
        };
    }

    function emitClientInfoError(requestContext, detail) {
        const eventDetail = {
            phase: "client-info",
            invocationId: requestContext?.invocationId || requestContext?.actionId || null,
            actionId: requestContext?.actionId || null,
            requestId: requestContext?.requestId || null,
            attempt: requestContext?.attempt || 0,
            sourceElement: requestContext?.sourceElement || null,
            ...detail
        };

        if (typeof emitLifecycle === "function") {
            emitLifecycle(
                requestContext?.sourceElement || null,
                "heimdall:client-info-error",
                eventDetail);
        }

        if (typeof dbg === "function")
            dbg("client information value omitted", eventDetail);
    }

    function serializedLength(value) {
        try {
            return JSON.stringify(value).length;
        } catch {
            return Number.POSITIVE_INFINITY;
        }
    }

    function jsonDepth(value) {
        if (!value || typeof value !== "object")
            return 0;

        const values = Array.isArray(value) ? value : Object.values(value);
        if (values.length === 0)
            return 1;

        return 1 + Math.max(...values.map(jsonDepth));
    }

    function tryReadStorage(area, key, requestContext) {
        let storage;
        try {
            storage = global[area === "local" ? "localStorage" : "sessionStorage"];
        } catch (error) {
            emitClientInfoError(requestContext, {
                code: "storage-area-unavailable",
                area,
                key: null,
                errorName: error?.name || "StorageError"
            });
            return { available: false, value: null };
        }

        if (!storage || typeof storage.getItem !== "function") {
            emitClientInfoError(requestContext, {
                code: "storage-area-unavailable",
                area,
                key: null,
                errorName: "StorageUnavailable"
            });
            return { available: false, value: null };
        }

        try {
            return { available: true, value: storage.getItem(key) };
        } catch (error) {
            emitClientInfoError(requestContext, {
                code: "storage-read-failed",
                area,
                key,
                errorName: error?.name || "StorageError"
            });
            return { available: true, value: null };
        }
    }

    function collectStorage(clientInfoConfig, requestContext) {
        const limits = clientInfoConfig.limits;
        const storage = {};
        let hasValues = false;

        for (const area of ["local", "session"]) {
            const configuredKeys = clientInfoConfig[area];
            const areaValues = {};

            for (let index = 0; index < configuredKeys.length; index++) {
                const key = configuredKeys[index];
                if (index >= limits.maxKeysPerArea) {
                    emitClientInfoError(requestContext, {
                        code: "storage-key-limit",
                        area,
                        key,
                        limit: limits.maxKeysPerArea
                    });
                    continue;
                }

                if (key.length > limits.maxKeyLength) {
                    emitClientInfoError(requestContext, {
                        code: "storage-key-too-long",
                        area,
                        key,
                        limit: limits.maxKeyLength
                    });
                    continue;
                }

                const result = tryReadStorage(area, key, requestContext);
                if (!result.available || result.value == null)
                    continue;

                if (result.value.length > limits.maxValueLength) {
                    emitClientInfoError(requestContext, {
                        code: "storage-value-too-long",
                        area,
                        key,
                        limit: limits.maxValueLength
                    });
                    continue;
                }

                const candidate = {
                    local: area === "local" ? { ...areaValues, [key]: result.value } : storage.local || {},
                    session: area === "session" ? { ...areaValues, [key]: result.value } : storage.session || {}
                };
                if (area === "local")
                    candidate.session = storage.session || {};
                else
                    candidate.local = storage.local || {};

                if (serializedLength(candidate) > limits.maxStorageChars) {
                    emitClientInfoError(requestContext, {
                        code: "storage-size-limit",
                        area,
                        key,
                        limit: limits.maxStorageChars
                    });
                    continue;
                }

                areaValues[key] = result.value;
                storage[area] = areaValues;
                hasValues = true;
            }
        }

        return hasValues ? storage : null;
    }

    function normalizeExtensions(info, requestContext, limits) {
        const source = info && info.extensions && typeof info.extensions === "object"
            ? info.extensions
            : {};
        const normalized = {};
        const entries = Object.entries(source);

        for (let index = 0; index < entries.length; index++) {
            const [key, value] = entries[index];
            if (index >= limits.maxExtensionEntries) {
                emitClientInfoError(requestContext, {
                    code: "extension-entry-limit",
                    area: null,
                    key,
                    limit: limits.maxExtensionEntries
                });
                continue;
            }

            if (jsonDepth(value) > limits.maxExtensionDepth) {
                emitClientInfoError(requestContext, {
                    code: "extension-depth-limit",
                    area: null,
                    key,
                    limit: limits.maxExtensionDepth
                });
                continue;
            }

            const candidate = { ...normalized, [key]: value };
            if (serializedLength(candidate) > limits.maxExtensionChars) {
                emitClientInfoError(requestContext, {
                    code: "extension-size-limit",
                    area: null,
                    key,
                    limit: limits.maxExtensionChars
                });
                continue;
            }

            normalized[key] = value;
        }

        return normalized;
    }

    function normalizeStorage(infoStorage, requestContext, limits) {
        const normalized = {};
        let hasValues = false;

        for (const area of ["local", "session"]) {
            const source = infoStorage && infoStorage[area] && typeof infoStorage[area] === "object"
                ? infoStorage[area]
                : {};
            const values = {};
            const entries = Object.entries(source);

            for (let index = 0; index < entries.length; index++) {
                const [key, value] = entries[index];
                if (index >= limits.maxKeysPerArea) {
                    emitClientInfoError(requestContext, {
                        code: "storage-key-limit",
                        area,
                        key,
                        limit: limits.maxKeysPerArea
                    });
                    continue;
                }

                if (key.length > limits.maxKeyLength) {
                    emitClientInfoError(requestContext, {
                        code: "storage-key-too-long",
                        area,
                        key,
                        limit: limits.maxKeyLength
                    });
                    continue;
                }

                if (typeof value !== "string") {
                    emitClientInfoError(requestContext, {
                        code: "storage-value-invalid",
                        area,
                        key,
                        limit: limits.maxValueLength
                    });
                    continue;
                }

                if (value.length > limits.maxValueLength) {
                    emitClientInfoError(requestContext, {
                        code: "storage-value-too-long",
                        area,
                        key,
                        limit: limits.maxValueLength
                    });
                    continue;
                }

                const candidate = {
                    local: area === "local" ? { ...values, [key]: value } : normalized.local || {},
                    session: area === "session" ? { ...values, [key]: value } : normalized.session || {}
                };
                if (serializedLength(candidate) > limits.maxStorageChars) {
                    emitClientInfoError(requestContext, {
                        code: "storage-size-limit",
                        area,
                        key,
                        limit: limits.maxStorageChars
                    });
                    continue;
                }

                values[key] = value;
                normalized[area] = values;
                hasValues = true;
            }
        }

        return hasValues ? normalized : {};
    }

    function prepareHeaderInfo(info, clientInfoConfig, requestContext) {
        if (!info || typeof info !== "object")
            return info;

        const limits = clientInfoConfig.limits;
        if (info.storage && typeof info.storage === "object")
            info.storage = normalizeStorage(info.storage, requestContext, limits);
        if (Object.prototype.hasOwnProperty.call(info, "extensions"))
            info.extensions = normalizeExtensions(info, requestContext, limits);
        return info;
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
        const clientInfoConfig = normalizeClientInfoConfig(config);
        if (!config || !clientInfoConfig.enabled)
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
            invocationId: requestContext?.invocationId || requestContext?.actionId || null,
            actionId: requestContext?.actionId || null,
            requestId: requestContext?.requestId || null,
            attempt: requestContext?.attempt || 0,
            sourceElement: requestContext?.sourceElement || null,
            info: cloneInfo(cachedInfo)
        };
        const storage = collectStorage(clientInfoConfig, requestContext);
        if (storage)
            detail.info.storage = storage;
        const shouldInclude = typeof emitLifecycle !== "function" || emitLifecycle(
            requestContext?.sourceElement || null,
            "heimdall:client-info-before",
            detail,
            { cancelable: true }
        );

        if (!shouldInclude || detail.info == null)
            return null;

        try {
            const prepared = prepareHeaderInfo(detail.info, clientInfoConfig, requestContext);
            let serialized = JSON.stringify(prepared);
            if (serialized.length > MAX_HEADER_LENGTH && prepared && typeof prepared === "object") {
                if (prepared.extensions && Object.keys(prepared.extensions).length > 0) {
                    delete prepared.extensions;
                    emitClientInfoError(requestContext, {
                        code: "header-size-limit",
                        area: null,
                        key: null,
                        dropped: "extensions",
                        limit: MAX_HEADER_LENGTH
                    });
                    serialized = JSON.stringify(prepared);
                }
                if (serialized.length > MAX_HEADER_LENGTH && prepared.storage) {
                    delete prepared.storage;
                    emitClientInfoError(requestContext, {
                        code: "header-size-limit",
                        area: null,
                        key: null,
                        dropped: "storage",
                        limit: MAX_HEADER_LENGTH
                    });
                    serialized = JSON.stringify(prepared);
                }
            }

            if (serialized.length > MAX_HEADER_LENGTH) {
                emitClientInfoError(requestContext, {
                    code: "header-size-limit",
                    area: null,
                    key: null,
                    dropped: "client-info",
                    limit: MAX_HEADER_LENGTH
                });
                return null;
            }

            return serialized;
        } catch (error) {
            emitClientInfoError(requestContext, {
                code: "serialization-failed",
                area: null,
                key: null,
                errorName: error?.name || "SerializationError"
            });
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
