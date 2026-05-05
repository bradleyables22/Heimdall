import {
    getAttr,
    intAttr,
    truthyAttr
} from "./utils.js";

export function createEventDelegates({
    getConfig,
    runActionFromElement
}) {
    function parseTokenList(value) {
        return String(value || "")
            .split(/\s+/)
            .map(x => x.trim().toLowerCase())
            .filter(Boolean);
    }

    function elementIgnoresTrigger(el, triggerName) {
        if (!el || !el.getAttribute)
            return false;

        const raw = getAttr(el, "heimdall-ignore");
        if (!raw)
            return false;

        const tokens = parseTokenList(raw);
        if (tokens.length === 0)
            return false;

        const trigger = String(triggerName || "").toLowerCase();
        return tokens.includes("*") || tokens.includes(trigger);
    }

    function getClosestIgnoreBoundary(target, triggerName) {
        let cur = target;
        while (cur && cur.nodeType === 1) {
            if (elementIgnoresTrigger(cur, triggerName))
                return cur;
            cur = cur.parentElement;
        }
        return null;
    }

    function matchesScope(actionEl, eventTarget) {
        const scope = (getAttr(actionEl, "heimdall-scope") || "closest").toLowerCase().trim();

        switch (scope) {
            case "self":
                return eventTarget === actionEl;
            case "closest":
            default:
                return true;
        }
    }

    function resolveActionElement(target, triggerAttr, triggerName) {
        if (!target || !target.closest)
            return null;

        const actionEl = target.closest(`[${triggerAttr}]`);
        if (!actionEl)
            return null;

        const ignoreBoundary = getClosestIgnoreBoundary(target, triggerName);

        // No ignore boundary, normal resolution.
        if (!ignoreBoundary)
            return matchesScope(actionEl, target) ? actionEl : null;

        // If the action is inside the ignore boundary, it is still allowed.
        if (ignoreBoundary.contains(actionEl))
            return matchesScope(actionEl, target) ? actionEl : null;

        // Otherwise the boundary blocks resolution past it.
        return null;
    }

    async function handleClick(e) {
        if (e.defaultPrevented)
            return;
        if (e.button != null && e.button !== 0)
            return;
        if (e.metaKey || e.ctrlKey || e.shiftKey || e.altKey)
            return;

        const el = resolveActionElement(e.target, "heimdall-content-click", "click");
        if (!el)
            return;

        const actionId = getAttr(el, "heimdall-content-click");
        if (!actionId)
            return;

        const isAnchor = el.tagName === "A";
        const preventDefault = truthyAttr(el, "heimdall-prevent-default", isAnchor);
        if (preventDefault)
            e.preventDefault();

        await runActionFromElement(el, actionId, "click");
    }

    async function handleChange(e) {
        if (e.defaultPrevented)
            return;

        const el = resolveActionElement(e.target, "heimdall-content-change", "change");
        if (!el)
            return;

        const actionId = getAttr(el, "heimdall-content-change");
        if (!actionId)
            return;

        const ms = intAttr(el, "heimdall-debounce", 0);
        if (ms > 0) {
            scheduleDebounced(el, "change", ms, () => {
                runActionFromElement(el, actionId, "change").catch(() => { /* logged */ });
            });
            return;
        }

        await runActionFromElement(el, actionId, "change");
    }

    const _debouncers = new WeakMap();

    function scheduleDebounced(el, key, ms, fn) {
        let map = _debouncers.get(el);
        if (!map) {
            map = new Map();
            _debouncers.set(el, map);
        }

        const prev = map.get(key);
        if (prev) clearTimeout(prev);

        const tid = setTimeout(() => {
            map.delete(key);
            fn();
        }, ms);

        map.set(key, tid);
    }

    async function handleInput(e) {
        if (e.defaultPrevented)
            return;

        const el = resolveActionElement(e.target, "heimdall-content-input", "input");
        if (!el)
            return;

        const actionId = getAttr(el, "heimdall-content-input");
        if (!actionId)
            return;

        const ms = intAttr(el, "heimdall-debounce", getConfig().inputDebounceMs || 250);

        if (ms > 0) {
            scheduleDebounced(el, "input", ms, () => {
                runActionFromElement(el, actionId, "input").catch(() => { /* logged */ });
            });
            return;
        }

        await runActionFromElement(el, actionId, "input");
    }

    async function handleSubmit(e) {
        if (e.defaultPrevented)
            return;

        const form = resolveActionElement(e.target, "heimdall-content-submit", "submit");
        if (!form)
            return;

        const actionId = getAttr(form, "heimdall-content-submit");
        if (!actionId)
            return;

        const preventDefault = truthyAttr(form, "heimdall-prevent-default", true);
        if (preventDefault)
            e.preventDefault();

        await runActionFromElement(form, actionId, "submit");
    }

    function normalizeKeySpec(spec) {
        return String(spec || "").trim();
    }

    function matchesKeySpec(e, spec) {
        const s = normalizeKeySpec(spec);
        if (!s)
            return true;

        if (/^\d+$/.test(s)) {
            const code = parseInt(s, 10);
            const kc = (e.keyCode != null ? e.keyCode : e.which);
            return kc === code;
        }

        return String(e.key || "").toLowerCase() === s.toLowerCase();
    }

    async function handleKeydown(e) {
        if (e.defaultPrevented)
            return;

        const el = resolveActionElement(e.target, "heimdall-content-keydown", "keydown");
        if (!el)
            return;

        const actionId = getAttr(el, "heimdall-content-keydown");
        if (!actionId)
            return;

        const keySpec = getAttr(el, "heimdall-key");
        if (keySpec && !matchesKeySpec(e, keySpec))
            return;

        const wantsPreventDefault = truthyAttr(
            el,
            "heimdall-prevent-default",
            (String(keySpec || "").toLowerCase() === "enter") &&
            (e.target && (e.target.tagName === "INPUT" || e.target.tagName === "TEXTAREA"))
        );

        if (wantsPreventDefault)
            e.preventDefault();

        await runActionFromElement(el, actionId, "keydown");
    }

    async function handleFocusOut(e) {
        if (e.defaultPrevented)
            return;

        const el = resolveActionElement(e.target, "heimdall-content-blur", "blur");
        if (!el)
            return;

        const actionId = getAttr(el, "heimdall-content-blur");
        if (!actionId)
            return;

        await runActionFromElement(el, actionId, "blur");
    }

    const _hoverTimers = new WeakMap();

    function isRealMouseEnter(e, el) {
        const from = e.relatedTarget;
        return !(from && (from === el || (el.contains && el.contains(from))));
    }

    async function handleMouseOver(e) {
        if (e.defaultPrevented)
            return;

        const el = resolveActionElement(e.target, "heimdall-content-hover", "hover");
        if (!el)
            return;
        if (!isRealMouseEnter(e, el))
            return;

        const actionId = getAttr(el, "heimdall-content-hover");
        if (!actionId)
            return;

        const delay = intAttr(el, "heimdall-hover-delay", getConfig().hoverDelayMs || 150);

        const prev = _hoverTimers.get(el);
        if (prev)
            clearTimeout(prev);

        if (delay > 0) {
            const tid = setTimeout(() => {
                _hoverTimers.delete(el);
                runActionFromElement(el, actionId, "hover").catch(() => { /* logged */ });
            }, delay);
            _hoverTimers.set(el, tid);
            return;
        }

        await runActionFromElement(el, actionId, "hover");
    }

    function handleMouseOut(e) {
        const el = resolveActionElement(e.target, "heimdall-content-hover", "hover");
        if (!el)
            return;

        const to = e.relatedTarget;
        if (to && (to === el || (el.contains && el.contains(to))))
            return;

        const tid = _hoverTimers.get(el);
        if (tid) {
            clearTimeout(tid);
            _hoverTimers.delete(el);
        }
    }

    return {
        handleChange,
        handleClick,
        handleFocusOut,
        handleInput,
        handleKeydown,
        handleMouseOut,
        handleMouseOver,
        handleSubmit
    };
}
