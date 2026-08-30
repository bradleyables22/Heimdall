import { getAttr } from "./utils.js";

export function createPageLifecycleRuntime({
    global,
    runActionFromElement,
    emit
}) {
    function invokeMatching(attr, triggerName) {
        const elements = document.querySelectorAll(`[${attr}]`);
        for (const el of elements) {
            const actionId = (getAttr(el, attr) || "").trim();
            if (!actionId)
                continue;

            runActionFromElement(el, actionId, triggerName).catch(() => { /* logged */ });
        }
    }

    function handleVisibilityChange() {
        if (document.visibilityState !== "visible")
            return;

        invokeMatching("heimdall-content-document-visible", "document-visible");
    }

    function handleOnline() {
        invokeMatching("heimdall-content-online", "online");
    }

    function handleOffline() {
        emit("heimdall:offline", { online: false });
    }

    function install() {
        if (document.__heimdallPageLifecycleInstalled)
            return;

        document.__heimdallPageLifecycleInstalled = true;
        document.addEventListener("visibilitychange", handleVisibilityChange, false);
        global.addEventListener("online", handleOnline, false);
        global.addEventListener("offline", handleOffline, false);
    }

    return { install };
}
