import { isElement } from "./utils.js";

const TIME_ATTRIBUTE = "heimdall-time";
const FORMAT_ATTRIBUTE = "heimdall-time-format";
const DEFAULT_FORMAT = "G";
const MAXIMUM_FORMAT_LENGTH = 256;
const MAXIMUM_CACHE_ENTRIES = 128;
const STANDARD_FORMATS = new Set(["d", "D", "t", "T", "g", "G"]);

export function createTimeLocalization({ global, emitLifecycle, dbg }) {
    const processedElements = new WeakMap();
    const formatterCache = new Map();
    const numberFormatterCache = new Map();
    const parsedFormatCache = new Map();
    let browserTimeZone = null;

    function setBoundedCache(cache, key, value) {
        if (cache.size >= MAXIMUM_CACHE_ENTRIES)
            cache.clear();
        cache.set(key, value);
        return value;
    }

    function getBrowserTimeZone() {
        if (browserTimeZone)
            return browserTimeZone;

        browserTimeZone = new Intl.DateTimeFormat().resolvedOptions().timeZone;
        if (!browserTimeZone)
            throw new Error("The browser did not provide a local timezone.");

        return browserTimeZone;
    }

    function findLanguage(element) {
        if (!element || typeof element.closest !== "function")
            return null;

        const languageElement = element.closest("[lang]");
        if (!languageElement)
            return null;

        const language = String(languageElement.getAttribute("lang") || "").trim();
        return language || null;
    }

    function resolveLocale(element, contextElement) {
        return findLanguage(element)
            || findLanguage(contextElement)
            || String(document.documentElement && document.documentElement.lang || "").trim()
            || String(global.navigator && global.navigator.language || "").trim()
            || "en-US";
    }

    function formatter(locale, timeZone, options) {
        const normalizedOptions = Object.assign({
            calendar: "gregory",
            timeZone
        }, options || {});
        const key = JSON.stringify([locale, normalizedOptions]);
        const cached = formatterCache.get(key);
        if (cached)
            return cached;

        return setBoundedCache(
            formatterCache,
            key,
            new Intl.DateTimeFormat(locale, normalizedOptions));
    }

    function numberFormatter(locale, minimumIntegerDigits) {
        const key = `${locale}\0${minimumIntegerDigits}`;
        const cached = numberFormatterCache.get(key);
        if (cached)
            return cached;

        return setBoundedCache(
            numberFormatterCache,
            key,
            new Intl.NumberFormat(locale, {
                minimumIntegerDigits,
                maximumFractionDigits: 0,
                useGrouping: false
            }));
    }

    function formatInteger(value, locale, minimumIntegerDigits) {
        return numberFormatter(locale, minimumIntegerDigits).format(value);
    }

    function formatPart(date, locale, timeZone, options, partName) {
        const part = formatter(locale, timeZone, options)
            .formatToParts(date)
            .find(candidate => candidate.type === partName);

        if (!part)
            throw new Error(`The browser could not format date/time part '${partName}'.`);

        return part.value;
    }

    function getZonedFields(date, timeZone) {
        const parts = formatter("en-US-u-ca-gregory-nu-latn", timeZone, {
            year: "numeric",
            month: "2-digit",
            day: "2-digit",
            hour: "2-digit",
            hourCycle: "h23",
            minute: "2-digit",
            second: "2-digit"
        }).formatToParts(date);
        const fields = {};

        for (const part of parts) {
            if (part.type === "year" || part.type === "month" || part.type === "day"
                || part.type === "hour" || part.type === "minute" || part.type === "second") {
                fields[part.type] = Number(part.value);
            }
        }

        for (const name of ["year", "month", "day", "hour", "minute", "second"]) {
            if (!Number.isFinite(fields[name]))
                throw new Error(`The browser could not resolve local date/time field '${name}'.`);
        }

        return fields;
    }

    function getOffsetMinutes(date, timeZone, fields) {
        const wallClockAsUtc = new Date(0);
        wallClockAsUtc.setUTCFullYear(fields.year, fields.month - 1, fields.day);
        wallClockAsUtc.setUTCHours(fields.hour, fields.minute, fields.second, 0);

        const instantWithoutMilliseconds = date.getTime() - date.getUTCMilliseconds();
        return Math.round((wallClockAsUtc.getTime() - instantWithoutMilliseconds) / 60000);
    }

    function formatOffset(date, timeZone, count, fields) {
        const offsetMinutes = getOffsetMinutes(date, timeZone, fields);
        const sign = offsetMinutes < 0 ? "-" : "+";
        const absoluteMinutes = Math.abs(offsetMinutes);
        const hours = Math.floor(absoluteMinutes / 60);
        const minutes = absoluteMinutes % 60;

        if (count === 1)
            return `${sign}${hours}`;
        if (count === 2)
            return `${sign}${String(hours).padStart(2, "0")}`;
        return `${sign}${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}`;
    }

    function isTokenLetter(value) {
        return value === "d" || value === "M" || value === "y"
            || value === "h" || value === "H" || value === "m"
            || value === "s" || value === "t" || value === "f"
            || value === "z";
    }

    function isSupportedToken(value, count) {
        switch (value) {
            case "d":
            case "M":
            case "y":
                return count >= 1 && count <= 4;
            case "h":
            case "H":
            case "m":
            case "s":
            case "t":
                return count >= 1 && count <= 2;
            case "f":
            case "z":
                return count >= 1 && count <= 3;
            default:
                return false;
        }
    }

    function isLetter(value) {
        return value.toLocaleUpperCase() !== value.toLocaleLowerCase();
    }

    function unsupportedFormat(format, token) {
        return new RangeError(
            `Local time format '${format}' contains unsupported token '${token}'. `
            + "Use d, D, t, T, g, or G, or a supported custom date/time format.");
    }

    function parseCustomFormat(format) {
        const cached = parsedFormatCache.get(format);
        if (cached)
            return cached;

        if (format.length > MAXIMUM_FORMAT_LENGTH)
            throw new RangeError(`Local time formats cannot exceed ${MAXIMUM_FORMAT_LENGTH} characters.`);

        const segments = [];
        let literal = "";

        function flushLiteral() {
            if (!literal)
                return;
            segments.push({ type: "literal", value: literal });
            literal = "";
        }

        for (let index = 0; index < format.length;) {
            const current = format[index];

            if (current === "'" || current === '"') {
                const quote = current;
                index++;
                let closed = false;

                while (index < format.length) {
                    const quoted = format[index];
                    if (quoted === "\\") {
                        if (index + 1 >= format.length)
                            break;
                        literal += format[index + 1];
                        index += 2;
                        continue;
                    }
                    if (quoted === quote) {
                        index++;
                        closed = true;
                        break;
                    }
                    literal += quoted;
                    index++;
                }

                if (!closed)
                    throw new RangeError("A local time format contains an unterminated quoted literal.");
                continue;
            }

            if (current === "\\") {
                if (index + 1 >= format.length)
                    throw new RangeError("A local time format cannot end with an escape character.");
                literal += format[index + 1];
                index += 2;
                continue;
            }

            if (current === "%") {
                if (index + 1 >= format.length)
                    throw new RangeError("A local time format cannot end with '%'.");

                const escapedToken = format[index + 1];
                if (!isSupportedToken(escapedToken, 1))
                    throw unsupportedFormat(format, escapedToken);

                flushLiteral();
                segments.push({ type: "token", value: escapedToken, count: 1 });
                index += 2;
                continue;
            }

            if (isTokenLetter(current)) {
                let count = 1;
                while (index + count < format.length && format[index + count] === current)
                    count++;

                if (!isSupportedToken(current, count))
                    throw unsupportedFormat(format, current);

                flushLiteral();
                segments.push({ type: "token", value: current, count });
                index += count;
                continue;
            }

            if (isLetter(current))
                throw unsupportedFormat(format, current);

            literal += current;
            index++;
        }

        flushLiteral();
        return setBoundedCache(parsedFormatCache, format, segments);
    }

    function formatStandard(date, format, locale, timeZone) {
        switch (format) {
            case "d":
                return formatter(locale, timeZone, { dateStyle: "short" }).format(date);
            case "D":
                return formatter(locale, timeZone, { dateStyle: "full" }).format(date);
            case "t":
                return formatter(locale, timeZone, { timeStyle: "short" }).format(date);
            case "T":
                return formatter(locale, timeZone, { timeStyle: "medium" }).format(date);
            case "g":
                return formatter(locale, timeZone, { dateStyle: "short", timeStyle: "short" }).format(date);
            case "G":
                return formatter(locale, timeZone, { dateStyle: "short", timeStyle: "medium" }).format(date);
            default:
                throw unsupportedFormat(format, format);
        }
    }

    function formatCustomToken(date, token, locale, timeZone, fields) {
        const { value, count } = token;

        switch (value) {
            case "d":
                if (count <= 2)
                    return formatInteger(fields.day, locale, count);
                return formatPart(date, locale, timeZone, {
                    weekday: count === 3 ? "short" : "long"
                }, "weekday");
            case "M":
                if (count <= 2)
                    return formatInteger(fields.month, locale, count);
                return formatPart(date, locale, timeZone, {
                    month: count === 3 ? "short" : "long"
                }, "month");
            case "y":
                if (count <= 2)
                    return formatInteger(fields.year % 100, locale, count);
                return formatInteger(fields.year, locale, count);
            case "H":
                return formatInteger(fields.hour, locale, count);
            case "h": {
                const twelveHour = fields.hour % 12 || 12;
                return formatInteger(twelveHour, locale, count);
            }
            case "m":
                return formatInteger(fields.minute, locale, count);
            case "s":
                return formatInteger(fields.second, locale, count);
            case "t": {
                const dayPeriod = formatPart(date, locale, timeZone, {
                    hour: "numeric",
                    hourCycle: "h12"
                }, "dayPeriod");
                return count === 1 ? Array.from(dayPeriod)[0] || "" : dayPeriod;
            }
            case "f":
                return String(date.getUTCMilliseconds()).padStart(3, "0").slice(0, count);
            case "z":
                return formatOffset(date, timeZone, count, fields);
            default:
                throw unsupportedFormat(token.value, token.value);
        }
    }

    function formatLocalTime(date, format, locale, timeZone) {
        if (STANDARD_FORMATS.has(format))
            return formatStandard(date, format, locale, timeZone);
        if (format.length === 1)
            throw unsupportedFormat(format, format);

        const segments = parseCustomFormat(format);
        const fields = getZonedFields(date, timeZone);
        let output = "";

        for (const segment of segments) {
            output += segment.type === "literal"
                ? segment.value
                : formatCustomToken(date, segment, locale, timeZone, fields);
        }

        return output;
    }

    function parseAbsoluteTime(value) {
        const normalized = String(value || "").trim();
        const absoluteIso = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,3})?(?:Z|[+-]\d{2}:\d{2})$/i;
        if (!absoluteIso.test(normalized))
            throw new RangeError("heimdall-time must contain an ISO timestamp with Z or an explicit offset.");

        const milliseconds = Date.parse(normalized);
        if (!Number.isFinite(milliseconds))
            throw new RangeError(`heimdall-time contains an invalid timestamp '${normalized}'.`);

        return { value: normalized, date: new Date(milliseconds) };
    }

    function signatureFor(value, format, timeZone, locale) {
        return `${value}\0${format}\0${timeZone}\0${locale}`;
    }

    function collectCandidates(root) {
        const candidates = [];
        if (isElement(root) && root.hasAttribute(TIME_ATTRIBUTE))
            candidates.push(root);

        if (root && typeof root.querySelectorAll === "function") {
            for (const element of root.querySelectorAll(`[${TIME_ATTRIBUTE}]`))
                candidates.push(element);
        }

        return candidates;
    }

    function localizeElement(element, options) {
        const rawValue = element.getAttribute(TIME_ATTRIBUTE);
        const rawFormat = element.getAttribute(FORMAT_ATTRIBUTE) || DEFAULT_FORMAT;
        let timeZone;
        let locale;
        let signature;

        try {
            timeZone = getBrowserTimeZone();
            locale = resolveLocale(element, options.contextElement);
            signature = signatureFor(rawValue, rawFormat, timeZone, locale);

            const existing = processedElements.get(element);
            if (existing && existing.signature === signature)
                return false;

            const detail = {
                element,
                value: rawValue,
                format: rawFormat,
                timeZone,
                locale,
                text: null,
                origin: options.origin || "boot",
                kind: options.kind || null
            };

            if (!emitLifecycle(element, "heimdall:time-before", detail, { cancelable: true })) {
                processedElements.set(element, { signature, status: "cancelled" });
                return false;
            }

            const parsed = parseAbsoluteTime(detail.value);
            const effectiveFormat = String(detail.format || DEFAULT_FORMAT);
            const effectiveTimeZone = String(detail.timeZone || timeZone);
            const effectiveLocale = String(detail.locale || locale);
            const text = detail.text == null
                ? formatLocalTime(parsed.date, effectiveFormat, effectiveLocale, effectiveTimeZone)
                : String(detail.text);

            element.textContent = text;
            processedElements.set(element, { signature, status: "localized" });

            emitLifecycle(element, "heimdall:time-after", {
                ...detail,
                value: parsed.value,
                format: effectiveFormat,
                timeZone: effectiveTimeZone,
                locale: effectiveLocale,
                text
            });

            return true;
        } catch (error) {
            if (signature)
                processedElements.set(element, { signature, status: "error" });

            emitLifecycle(element, "heimdall:time-error", {
                element,
                value: rawValue,
                format: rawFormat,
                timeZone: timeZone || null,
                locale: locale || null,
                error,
                origin: options.origin || "boot",
                kind: options.kind || null
            });
            dbg("local time formatting failed", { element, error });
            return false;
        }
    }

    function localize(root, options = {}) {
        let count = 0;
        for (const element of collectCandidates(root)) {
            if (localizeElement(element, options))
                count++;
        }
        return count;
    }

    return {
        localize
    };
}
