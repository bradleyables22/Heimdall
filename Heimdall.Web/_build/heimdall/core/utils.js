export function isElement(x) {
    return x && x.nodeType === 1;
}

export function resolveTarget(target, fallbackEl) {
    if (!target)
        return fallbackEl || null;
    if (isElement(target))
        return target;
    if (typeof target === "string")
        return document.querySelector(target);
    return fallbackEl || null;
}

export function safeJsonParse(text) {
    try {
        return JSON.parse(text);
    }
    catch {
        return null;
    }
}

export async function safeText(res) {
    try {
        return await res.text();
    }
    catch {
        return "";
    }
}

export function onReady(fn) {
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", fn, { once: true });
    } else {
        fn();
    }
}

export function getAttr(el, name) {
    const v = el.getAttribute(name);
    return v == null ? null : v;
}

export function truthyAttr(el, name, defaultValue) {
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

export function intAttr(el, name, defaultValue) {
    const v = getAttr(el, name);
    if (v == null || v === "")
        return defaultValue;

    const n = parseInt(v, 10);
    return Number.isFinite(n) ? n : defaultValue;
}

export function formDataToObject(fd) {
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

export function getByPath(root, path) {
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
