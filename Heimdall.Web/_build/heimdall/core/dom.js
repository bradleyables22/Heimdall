import {
    getAttr,
    isElement,
    resolveTarget
} from "./utils.js";

export function createDomPipeline({ getConfig, boot, dbg, jsInvokeVoid }) {
    function stripScripts(rootNode) {
        if (!rootNode || !rootNode.querySelectorAll)
            return;
        const scripts = rootNode.querySelectorAll("script");
        for (const s of scripts)
            s.remove();
    }
    function containsTag(html, tagName) {
        if (!html)
            return false;

        return html.toLowerCase().indexOf("<" + tagName.toLowerCase()) !== -1;
    }
    function parseHtmlToTemplate(html) {
        const tpl = document.createElement("template");
        tpl.innerHTML = html || "";
        stripScripts(tpl.content);
        return tpl;
    }

    function fragmentToNodesArray(fragment) {
        return Array.from(fragment.childNodes || []);
    }

    function applySwap(targetEl, fragment, swap) {
        const mode = (swap || "inner").toLowerCase();

        if (mode === "none")
            return { didApply: false, appliedRoot: null };
        if (!targetEl)
            return { didApply: false, appliedRoot: null };

        const nodes = fragmentToNodesArray(fragment);
        const firstElement = nodes.find(n => n && n.nodeType === 1) || null;
        const appliedRoot = firstElement || targetEl;

        switch (mode) {
            case "outer": {
                if (nodes.length === 0) {
                    // FIX: capture parentElement BEFORE remove() detaches the node.
                    const parent = targetEl.parentElement;
                    targetEl.remove();
                    return { didApply: true, appliedRoot: parent || null };
                }
                targetEl.replaceWith(...nodes);
                return { didApply: true, appliedRoot };
            }
            case "beforeend":
                targetEl.append(...nodes);
                return { didApply: true, appliedRoot };
            case "afterbegin":
                targetEl.prepend(...nodes);
                return { didApply: true, appliedRoot };
            default: // "inner"
                targetEl.replaceChildren(...nodes);
                return { didApply: true, appliedRoot };
        }
    }

    function stripInvocationsFromFragment(fragment) {
        if (!fragment || !fragment.querySelectorAll)
            return;
        const invs = fragment.querySelectorAll("invocation");
        for (const inv of invs)
            inv.remove();
    }

    function stripAbortsFromFragment(fragment) {
        if (!fragment || !fragment.querySelectorAll)
            return;
        const aborts = fragment.querySelectorAll("abort");
        for (const abortEl of aborts)
            abortEl.remove();
    }

    function stripRedirectsFromFragment(fragment) {
        if (!fragment || !fragment.querySelectorAll)
            return;
        const redirects = fragment.querySelectorAll("redirect");
        for (const redirectEl of redirects)
            redirectEl.remove();
    }

    function normalizeJsInvokeTiming(value) {
        return jsInvokeVoid && typeof jsInvokeVoid.normalizeTiming === "function"
            ? jsInvokeVoid.normalizeTiming(value)
            : (String(value || "after").toLowerCase().trim() === "before" ? "before" : "after");
    }

    function collectJsInvokeVoidDirectives(rootNode) {
        const directives = [];

        function visit(node) {
            if (!node || !node.querySelectorAll)
                return;

            const jsEls = [];
            if (isElement(node) && String(node.localName || "").toLowerCase() === "javascript")
                jsEls.push(node);

            for (const jsEl of Array.from(node.querySelectorAll("javascript")))
                jsEls.push(jsEl);

            for (const jsEl of jsEls) {
                directives.push({
                    functionPath: getAttr(jsEl, "function"),
                    argsJson: getAttr(jsEl, "args"),
                    timing: normalizeJsInvokeTiming(getAttr(jsEl, "timing")),
                    sourceEl: jsEl
                });

                jsEl.remove();
            }

            for (const tpl of Array.from(node.querySelectorAll("template"))) {
                if (tpl.content)
                    visit(tpl.content);
            }
        }

        visit(rootNode);
        return directives;
    }

    function stripJsInvokeVoidFromFragment(fragment) {
        collectJsInvokeVoidDirectives(fragment);
    }

    function splitJsInvokeVoidDirectives(directives) {
        const before = [];
        const after = [];

        for (const directive of directives || []) {
            if (normalizeJsInvokeTiming(directive.timing) === "before")
                before.push(directive);
            else
                after.push(directive);
        }

        return { before, after };
    }

    function invokeJsInvokeVoidDirectives(directives, context) {
        if (!jsInvokeVoid || typeof jsInvokeVoid.invokeAll !== "function")
            return 0;

        return jsInvokeVoid.invokeAll(directives, context);
    }

    function extractRedirectFromFragment(fragment) {
        if (!fragment || !fragment.querySelector)
            return null;

        const redirectEl = fragment.querySelector("redirect");
        if (!redirectEl)
            return null;

        const urlAttr = getAttr(redirectEl, "url");
        if (urlAttr && urlAttr.trim())
            return { url: urlAttr.trim() };

        const textUrl = (redirectEl.textContent || "").trim();
        if (textUrl)
            return { url: textUrl };

        return null;
    }

    function fragmentToHtml(fragment) {
        const host = document.createElement("div");
        try {
            host.append(fragment.cloneNode(true));
        } catch {
            const frag = fragment && fragment.cloneNode ? fragment.cloneNode(true) : null;
            if (frag && frag.childNodes)
                host.append(...Array.from(frag.childNodes));
        }
        return host.innerHTML;
    }

    function sanitizeHtmlStringNoApply(html) {
        if (!html)
            return html;

        const hasScript = containsTag(html, "script");
        const hasInv = containsTag(html, "invocation");
        const hasAbort = containsTag(html, "abort");
        const hasRedirect = containsTag(html, "redirect");
        const hasJsInvokeVoid = containsTag(html, "javascript");

        if (!hasScript && !hasInv && !hasAbort && !hasRedirect && !hasJsInvokeVoid)
            return html;

        const tpl = parseHtmlToTemplate(html);
        stripInvocationsFromFragment(tpl.content);
        stripAbortsFromFragment(tpl.content);
        stripRedirectsFromFragment(tpl.content);
        stripJsInvokeVoidFromFragment(tpl.content);
        return fragmentToHtml(tpl.content);
    }

    function processOob(html, sourceEl, context) {
        const hasScript = containsTag(html, "script");
        const hasInv = containsTag(html, "invocation");
        const hasAbort = containsTag(html, "abort");
        const hasRedirect = containsTag(html, "redirect");
        const hasJsInvokeVoid = containsTag(html, "javascript");

        if (!hasInv && !hasScript && !hasAbort && !hasRedirect && !hasJsInvokeVoid) {
            return {
                html: html || "",
                applied: 0,
                abortSwap: false,
                abortReason: null,
                redirectUrl: null,
                jsAfter: []
            };
        }

        const tpl = parseHtmlToTemplate(html);
        const fragment = tpl.content;

        const redirect = extractRedirectFromFragment(fragment);
        if (redirect && redirect.url) {
            stripRedirectsFromFragment(fragment);
            return {
                html: fragmentToHtml(fragment),
                applied: 0,
                abortSwap: true,
                abortReason: "redirect",
                redirectUrl: redirect.url,
                jsAfter: []
            };
        }

        const jsDirectives = collectJsInvokeVoidDirectives(fragment);
        const jsGroups = splitJsInvokeVoidDirectives(jsDirectives);
        invokeJsInvokeVoidDirectives(jsGroups.before, Object.assign({ phase: "before", sourceEl }, context || {}));

        const aborts = fragment.querySelectorAll("abort");
        let abortSwap = false;
        let abortReason = null;

        if (aborts && aborts.length > 0) {
            abortSwap = true;
            for (const abortEl of Array.from(aborts)) {
                const reason = getAttr(abortEl, "reason");
                if (abortReason == null && reason && reason.trim())
                    abortReason = reason.trim();
                abortEl.remove();
            }
        }

        const invocations = fragment.querySelectorAll("invocation");
        if (!invocations || invocations.length === 0) {
            return {
                html: fragmentToHtml(fragment),
                applied: 0,
                abortSwap,
                abortReason,
                redirectUrl: null,
                jsAfter: jsGroups.after
            };
        }

        if (!getConfig().oobEnabled) {
            stripInvocationsFromFragment(fragment);
            return {
                html: fragmentToHtml(fragment),
                applied: 0,
                abortSwap,
                abortReason,
                redirectUrl: null,
                jsAfter: jsGroups.after
            };
        }

        let applied = 0;

        for (const invEl of Array.from(invocations)) {
            const targetSel = getAttr(invEl, "heimdall-content-target");
            if (!targetSel) {
                dbg("Invocation missing heimdall-content-target; stripping", invEl);
                invEl.remove();
                continue;
            }

            const swap = (getAttr(invEl, "heimdall-content-swap") || "inner").toLowerCase();
            const targetEl = resolveTarget(targetSel, null);

            if (!targetEl) {
                dbg("Invocation target not found; stripping", targetSel);
                invEl.remove();
                continue;
            }

            if (swap !== "none") {
                const payloadTemplate = invEl.querySelector("template");

                let payloadFrag;
                if (payloadTemplate && payloadTemplate.content) {
                    payloadFrag = payloadTemplate.content.cloneNode(true);
                } else {
                    payloadFrag = parseHtmlToTemplate(invEl.innerHTML || "").content;
                }

                stripScripts(payloadFrag);

                const { didApply, appliedRoot } = applySwap(targetEl, payloadFrag, swap);
                if (didApply) {
                    applied++;
                    if (!getConfig().observeDom) {
                        try {
                            boot(appliedRoot || targetEl);
                        }
                        catch { /* ignore */ }
                    }
                }
            }

            invEl.remove();
        }

        return {
            html: fragmentToHtml(fragment),
            applied,
            abortSwap,
            abortReason,
            redirectUrl: null,
            jsAfter: jsGroups.after
        };
    }

    return {
        applySwap,
        parseHtmlToTemplate,
        processOob,
        sanitizeHtmlStringNoApply,
        invokeJsInvokeVoidDirectives,
        stripAbortsFromFragment,
        stripInvocationsFromFragment,
        stripJsInvokeVoidFromFragment,
        stripRedirectsFromFragment
    };
}
