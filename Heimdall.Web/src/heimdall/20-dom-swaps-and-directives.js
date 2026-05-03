
    function stripScripts(rootNode) {
        if (!rootNode || !rootNode.querySelectorAll)
            return;
        const scripts = rootNode.querySelectorAll("script");
        for (const s of scripts)
            s.remove();
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

        const hasScript = html.indexOf("<script") !== -1 || html.indexOf("<SCRIPT") !== -1;
        const hasInv = html.indexOf("<Invocation") !== -1 || html.indexOf("<invocation") !== -1;
        const hasAbort = html.indexOf("<Abort") !== -1 || html.indexOf("<abort") !== -1;
        const hasRedirect = html.indexOf("<Redirect") !== -1 || html.indexOf("<redirect") !== -1;

        if (!hasScript && !hasInv && !hasAbort && !hasRedirect)
            return html;

        const tpl = parseHtmlToTemplate(html);
        stripInvocationsFromFragment(tpl.content);
        stripAbortsFromFragment(tpl.content);
        stripRedirectsFromFragment(tpl.content);
        return fragmentToHtml(tpl.content);
    }

    function processOob(html, sourceEl) {
        const hasInv = html && (html.indexOf("<Invocation") !== -1 || html.indexOf("<invocation") !== -1);
        const hasScript = html && (html.indexOf("<script") !== -1 || html.indexOf("<SCRIPT") !== -1);
        const hasAbort = html && (html.indexOf("<Abort") !== -1 || html.indexOf("<abort") !== -1);
        const hasRedirect = html && (html.indexOf("<Redirect") !== -1 || html.indexOf("<redirect") !== -1);

        if (!hasInv && !hasScript && !hasAbort && !hasRedirect) {
            return {
                html: html || "",
                applied: 0,
                abortSwap: false,
                abortReason: null,
                redirectUrl: null
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
                redirectUrl: redirect.url
            };
        }

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
                redirectUrl: null
            };
        }

        if (!Heimdall.config.oobEnabled) {
            stripInvocationsFromFragment(fragment);
            return {
                html: fragmentToHtml(fragment),
                applied: 0,
                abortSwap,
                abortReason,
                redirectUrl: null
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
                    if (!Heimdall.config.observeDom) {
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
            redirectUrl: null
        };
    }

