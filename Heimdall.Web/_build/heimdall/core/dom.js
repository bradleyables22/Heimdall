import {
    getAttr,
    isElement,
    resolveTarget
} from "./utils.js";

export function createDomPipeline({ getConfig, boot, dbg, emitLifecycle, jsInvokeVoid, timeLocalization, mutations }) {
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

    function applySwap(targetEl, fragment, swap, lifecycleContext) {
        lifecycleContext = lifecycleContext || {};
        let mode = String(swap || "inner").toLowerCase();

        if (mode === "none")
            return { didApply: false, appliedRoot: null, target: targetEl, swap: mode, cancelled: false };
        if (!targetEl)
            return { didApply: false, appliedRoot: null, target: null, swap: mode, cancelled: false };

        stripScripts(fragment);

        const detail = {
            origin: lifecycleContext.kind || "action",
            kind: lifecycleContext.swapKind || "main",
            target: targetEl,
            fragment,
            swap: mode,
            sourceElement: lifecycleContext.sourceEl || null,
            requestContext: lifecycleContext.requestContext || null
        };

        if (emitLifecycle && !emitLifecycle(
            detail.sourceElement,
            "heimdall:swap-before",
            detail,
            { cancelable: true }
        )) {
            return { didApply: false, appliedRoot: null, target: targetEl, swap: mode, cancelled: true };
        }

        targetEl = resolveTarget(detail.target, targetEl);
        fragment = detail.fragment && detail.fragment.childNodes ? detail.fragment : fragment;
        mode = String(detail.swap || mode).toLowerCase();

        if (mode === "none" || !targetEl)
            return { didApply: false, appliedRoot: null, target: targetEl, swap: mode, cancelled: false };

        stripScripts(fragment);

        if (timeLocalization && typeof timeLocalization.localize === "function") {
            const contextElement = mode === "outer"
                ? targetEl.parentElement
                : targetEl;
            timeLocalization.localize(fragment, {
                origin: detail.origin,
                kind: detail.kind,
                contextElement
            });
        }

        const nodes = fragmentToNodesArray(fragment);
        const firstElement = nodes.find(n => n && n.nodeType === 1) || null;
        const appliedRoot = firstElement || targetEl;
        let result;

        switch (mode) {
            case "outer": {
                if (nodes.length === 0) {
                    // FIX: capture parentElement BEFORE remove() detaches the node.
                    const parent = targetEl.parentElement;
                    targetEl.remove();
                    result = { didApply: true, appliedRoot: parent || null, target: targetEl, swap: mode, cancelled: false };
                    break;
                }
                targetEl.replaceWith(...nodes);
                result = { didApply: true, appliedRoot, target: targetEl, swap: mode, cancelled: false };
                break;
            }
            case "beforeend":
                targetEl.append(...nodes);
                result = { didApply: true, appliedRoot, target: targetEl, swap: mode, cancelled: false };
                break;
            case "afterbegin":
                targetEl.prepend(...nodes);
                result = { didApply: true, appliedRoot, target: targetEl, swap: mode, cancelled: false };
                break;
            default: // "inner"
                targetEl.replaceChildren(...nodes);
                result = { didApply: true, appliedRoot, target: targetEl, swap: mode, cancelled: false };
                break;
        }

        if (emitLifecycle) {
            emitLifecycle(detail.sourceElement, "heimdall:swap-after", {
                ...detail,
                target: targetEl,
                swap: mode,
                nodes,
                appliedRoot: result.appliedRoot
            });
        }

        return result;
    }

    function stripInvocationsFromFragment(fragment) {
        if (!fragment || !fragment.querySelectorAll)
            return;
        const invs = fragment.querySelectorAll("invocation");
        for (const inv of invs)
            inv.remove();
    }

    function stripMutationsFromFragment(fragment) {
        if (!fragment || !fragment.querySelectorAll)
            return;
        const directives = fragment.querySelectorAll("mutation");
        for (const directive of directives)
            directive.remove();
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

    function stripHistoryFromFragment(fragment) {
        if (!fragment || !fragment.querySelectorAll)
            return;
        const directives = fragment.querySelectorAll("history");
        for (const directive of directives)
            directive.remove();
    }

    function extractHistoryFromFragment(fragment) {
        if (!fragment || !fragment.querySelectorAll)
            return null;

        const directives = Array.from(fragment.querySelectorAll("history"));
        if (directives.length === 0)
            return null;
        if (directives.length > 1) {
            stripHistoryFromFragment(fragment);
            return { error: "A Heimdall response can contain only one history directive." };
        }

        const directive = directives[0];
        const command = {
            mode: getAttr(directive, "mode"),
            url: getAttr(directive, "url") || (directive.textContent || "").trim()
        };
        directive.remove();
        return command;
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
        const hasMutation = containsTag(html, "mutation");
        const hasHistory = containsTag(html, "history");

        if (!hasScript && !hasInv && !hasAbort && !hasRedirect && !hasJsInvokeVoid && !hasMutation && !hasHistory)
            return html;

        const tpl = parseHtmlToTemplate(html);
        stripInvocationsFromFragment(tpl.content);
        stripAbortsFromFragment(tpl.content);
        stripRedirectsFromFragment(tpl.content);
        stripJsInvokeVoidFromFragment(tpl.content);
        stripMutationsFromFragment(tpl.content);
        stripHistoryFromFragment(tpl.content);
        return fragmentToHtml(tpl.content);
    }

    function processOob(html, sourceEl, context) {
        const hasScript = containsTag(html, "script");
        const hasInv = containsTag(html, "invocation");
        const hasAbort = containsTag(html, "abort");
        const hasRedirect = containsTag(html, "redirect");
        const hasJsInvokeVoid = containsTag(html, "javascript");
        const hasMutation = containsTag(html, "mutation");
        const hasHistory = containsTag(html, "history");

        if (!hasInv && !hasScript && !hasAbort && !hasRedirect && !hasJsInvokeVoid && !hasMutation && !hasHistory) {
            return {
                html: html || "",
                applied: 0,
                abortSwap: false,
                abortReason: null,
                redirectUrl: null,
                jsAfter: [],
                mutationTargets: [],
                historyCommand: null
            };
        }

        const tpl = parseHtmlToTemplate(html);
        const fragment = tpl.content;

        const redirect = extractRedirectFromFragment(fragment);
        if (redirect && redirect.url) {
            stripRedirectsFromFragment(fragment);
            stripHistoryFromFragment(fragment);
            return {
                html: fragmentToHtml(fragment),
                applied: 0,
                abortSwap: true,
                abortReason: "redirect",
                redirectUrl: redirect.url,
                jsAfter: [],
                mutationTargets: [],
                historyCommand: null
            };
        }

        const historyCommand = extractHistoryFromFragment(fragment);

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

        const commands = fragment.querySelectorAll("invocation,mutation");
        if (!commands || commands.length === 0) {
            return {
                html: fragmentToHtml(fragment),
                applied: 0,
                abortSwap,
                abortReason,
                redirectUrl: null,
                jsAfter: jsGroups.after,
                mutationTargets: [],
                historyCommand
            };
        }

        if (!getConfig().oobEnabled) {
            stripInvocationsFromFragment(fragment);
            stripMutationsFromFragment(fragment);
            return {
                html: fragmentToHtml(fragment),
                applied: 0,
                abortSwap,
                abortReason,
                redirectUrl: null,
                jsAfter: jsGroups.after,
                mutationTargets: [],
                historyCommand
            };
        }

        let applied = 0;
        const mutationTargets = [];

        for (const commandEl of Array.from(commands)) {
            if (String(commandEl.localName || "").toLowerCase() === "mutation") {
                const result = mutations.apply(commandEl, sourceEl, context || {});
                if (result && result.applied) {
                    applied++;
                    mutationTargets.push(...(result.reconcileTargets || []));
                }
                commandEl.remove();
                continue;
            }

            const invEl = commandEl;
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
                stripInvocationsFromFragment(payloadFrag);
                stripMutationsFromFragment(payloadFrag);
                stripHistoryFromFragment(payloadFrag);

                const swapResult = applySwap(targetEl, payloadFrag, swap, Object.assign({}, context || {}, {
                    sourceEl,
                    swapKind: "invocation"
                }));
                const { didApply, appliedRoot } = swapResult;
                if (didApply) {
                    applied++;
                    if (!getConfig().observeDom) {
                        try {
                            boot(appliedRoot || swapResult.target || targetEl);
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
            jsAfter: jsGroups.after,
            mutationTargets,
            historyCommand
        };
    }

    function reconcileMutations(targets) {
        if (mutations && typeof mutations.reconcile === "function")
            mutations.reconcile(targets);
    }

    return {
        applySwap,
        parseHtmlToTemplate,
        processOob,
        reconcileMutations,
        sanitizeHtmlStringNoApply,
        invokeJsInvokeVoidDirectives,
        stripAbortsFromFragment,
        stripInvocationsFromFragment,
        stripMutationsFromFragment,
        stripJsInvokeVoidFromFragment,
        stripRedirectsFromFragment,
        stripHistoryFromFragment
    };
}
