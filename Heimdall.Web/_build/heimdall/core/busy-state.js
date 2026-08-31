export function createBusyStateManager() {
    const states = new WeakMap();

    function acquire(el) {
        if (!el)
            return;

        let state = states.get(el);
        if (!state) {
            state = {
                count: 0,
                hadDisabled: false,
                hadAriaBusy: false,
                ariaBusy: null
            };
            states.set(el, state);
        }

        if (state.count === 0) {
            state.hadDisabled = el.hasAttribute("disabled");
            state.hadAriaBusy = el.hasAttribute("aria-busy");
            state.ariaBusy = el.getAttribute("aria-busy");
        }

        state.count++;
        el.setAttribute("disabled", "disabled");
        el.setAttribute("aria-busy", "true");
    }

    function release(el) {
        if (!el)
            return;

        const state = states.get(el);
        if (!state)
            return;

        state.count = Math.max(0, state.count - 1);
        if (state.count > 0)
            return;

        if (state.hadDisabled)
            el.setAttribute("disabled", "disabled");
        else
            el.removeAttribute("disabled");

        if (state.hadAriaBusy)
            el.setAttribute("aria-busy", state.ariaBusy == null ? "" : state.ariaBusy);
        else
            el.removeAttribute("aria-busy");

        states.delete(el);
    }

    // A response mutation is authoritative. Update the state that will be restored
    // after the request, then reassert the temporary busy overlay until release().
    function rebaseAttribute(el, name, present, value) {
        const state = el ? states.get(el) : null;
        if (!state || state.count <= 0)
            return;

        const normalized = String(name || "").toLowerCase();
        if (normalized === "disabled") {
            state.hadDisabled = !!present;
            el.setAttribute("disabled", "disabled");
            return;
        }

        if (normalized === "aria-busy") {
            state.hadAriaBusy = !!present;
            state.ariaBusy = present ? String(value == null ? "" : value) : null;
            el.setAttribute("aria-busy", "true");
        }
    }

    return {
        acquire,
        rebaseAttribute,
        release
    };
}
