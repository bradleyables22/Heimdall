import {
    formDataToPayload,
    getAttr,
    getByPath,
    safeJsonParse
} from "./utils.js";

export function createPayloadResolver(global) {
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
            return formDataToPayload(new FormData(form));
        }

        if (from === "self") {
            const obj = {};
            for (const key in el.dataset) obj[key] = el.dataset[key];
            return obj;
        }

        const form = document.querySelector(fromRaw);
        if (form && form.tagName === "FORM") {
            return formDataToPayload(new FormData(form));
        }

        return null;
    }

    return {
        payloadFromElement
    };
}
