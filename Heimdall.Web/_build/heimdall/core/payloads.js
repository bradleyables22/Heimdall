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

    function readStateAttribute(host, attr) {
        if (!host || !host.hasAttribute || !host.hasAttribute(attr))
            return null;

        const raw = host.getAttribute(attr);
        if (!raw)
            return null;

        return safeJsonParse(raw);
    }

    function createClosestStateBinding(el, key) {
        const host = findClosestStateElement(el, key);
        if (!host)
            return null;

        const attribute = key ? `data-heimdall-state-${key}` : "data-heimdall-state";
        return {
            kind: "closest-state",
            host,
            attribute,
            value: readStateAttribute(host, attribute),
            resolve() {
                if (host.isConnected === false || !host.hasAttribute(attribute)) {
                    return {
                        available: false,
                        value: null
                    };
                }

                return {
                    available: true,
                    value: readStateAttribute(host, attribute)
                };
            }
        };
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

    function payloadBindingFromElement(el) {
        const payloadAttr = getAttr(el, "heimdall-payload");
        if (payloadAttr)
            return { value: safeJsonParse(payloadAttr), binding: null };

        const refObj = resolvePayloadRef(el);
        if (refObj !== undefined)
            return { value: refObj, binding: null };

        const fromRaw = (getAttr(el, "heimdall-payload-from") || "").trim();
        const from = fromRaw.toLowerCase();

        // closest-state[:key]
        if (from === "closest-state" || from.startsWith("closest-state:")) {
            const key = from.startsWith("closest-state:")
                ? fromRaw.substring("closest-state:".length).trim()
                : null;
            const binding = createClosestStateBinding(el, key || null);
            return binding
                ? { value: binding.value, binding }
                : { value: null, binding: null };
        }

        if (!from)
            return { value: null, binding: null };

        if (from === "closest-form") {
            const form = el.closest("form");
            if (!form)
                return { value: null, binding: null };
            return { value: formDataToPayload(new FormData(form)), binding: null };
        }

        if (from === "self") {
            const obj = {};
            for (const key in el.dataset) obj[key] = el.dataset[key];
            return { value: obj, binding: null };
        }

        const form = document.querySelector(fromRaw);
        if (form && form.tagName === "FORM") {
            return { value: formDataToPayload(new FormData(form)), binding: null };
        }

        return { value: null, binding: null };
    }

    function payloadFromElement(el) {
        return payloadBindingFromElement(el).value;
    }

    return {
        payloadBindingFromElement,
        payloadFromElement
    };
}
