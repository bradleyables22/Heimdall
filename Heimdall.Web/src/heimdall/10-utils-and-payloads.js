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

        // closest-state[:key]
        if (from === "closest-state" || from.startsWith("closest-state:")) {
            const key = from.startsWith("closest-state:")
                ? fromRaw.substring("closest-state:".length).trim()
                : null;
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
            console.debug(`[Heimdall]`, ...args);
        }
    }
