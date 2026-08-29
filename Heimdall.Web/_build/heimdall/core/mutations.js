import { getAttr, isElement } from "./utils.js";

export function createMutationRuntime({ global, emitLifecycle, dbg, boot, busyState }) {
    function mutationError(sourceEl, directive, context, code, message, error) {
        const detail = {
            origin: context && context.kind ? context.kind : "action",
            sourceElement: sourceEl || null,
            requestContext: context && context.requestContext ? context.requestContext : null,
            directive,
            code,
            message,
            error: error || null
        };

        if (emitLifecycle)
            emitLifecycle(sourceEl, "heimdall:mutation-error", detail);
        dbg("mutation error", detail);
        return { applied: false, cancelled: false, targets: [], error: detail };
    }

    function validateAttributeName(name) {
        const probe = document.createElement("div");
        probe.setAttribute(name, "");
    }

    function classTokens(raw) {
        return String(raw || "")
            .split(/\s+/)
            .map(token => token.trim())
            .filter(Boolean);
    }

    function parseOperations(directive) {
        const operations = [];

        for (const child of Array.from(directive.children || [])) {
            const tag = String(child.localName || "").toLowerCase();

            if (tag === "mutation-attr") {
                const name = (getAttr(child, "name") || "").trim();
                if (!name)
                    throw new Error("mutation-attr requires a non-empty name attribute.");
                validateAttributeName(name);

                operations.push(child.hasAttribute("value")
                    ? { type: "attribute", action: "set", name, value: child.getAttribute("value") || "" }
                    : { type: "attribute", action: "remove", name, value: null });
                continue;
            }

            if (tag === "mutation-class") {
                const hasAdd = child.hasAttribute("add");
                const hasRemove = child.hasAttribute("remove");
                if (hasAdd === hasRemove)
                    throw new Error("mutation-class requires exactly one of add or remove.");

                const action = hasAdd ? "add" : "remove";
                const tokens = classTokens(child.getAttribute(action));
                if (tokens.length === 0)
                    throw new Error(`mutation-class ${action} requires at least one class token.`);

                operations.push({ type: "class", action, tokens });
                continue;
            }

            throw new Error(`Unsupported mutation operation '${tag || "unknown"}'.`);
        }

        return operations;
    }

    function resolveRoots(targetSelector, allTargets) {
        return allTargets
            ? Array.from(document.querySelectorAll(targetSelector))
            : [document.querySelector(targetSelector)].filter(Boolean);
    }

    function resolveTargets(roots, scope, selector) {
        const targets = [];
        const seen = new Set();

        function add(el) {
            if (!isElement(el) || seen.has(el))
                return;
            seen.add(el);
            targets.push(el);
        }

        for (const root of roots) {
            if (scope === "self") {
                add(root);
                continue;
            }

            if (scope === "subtree") {
                add(root);
                for (const el of root.querySelectorAll("*"))
                    add(el);
                continue;
            }

            for (const el of root.querySelectorAll(selector))
                add(el);
        }

        return targets;
    }

    function applyOperation(target, operation) {
        if (operation.type === "attribute") {
            if (operation.action === "set")
                target.setAttribute(operation.name, operation.value);
            else
                target.removeAttribute(operation.name);

            if (busyState && typeof busyState.rebaseAttribute === "function") {
                busyState.rebaseAttribute(
                    target,
                    operation.name,
                    operation.action === "set",
                    operation.value);
            }
            return;
        }

        if (operation.action === "add")
            target.classList.add(...operation.tokens);
        else
            target.classList.remove(...operation.tokens);
    }

    function requiresBehaviorReconciliation(operation) {
        if (!operation || operation.type !== "attribute")
            return false;

        const name = String(operation.name || "").toLowerCase();
        return name === "lang" || name.startsWith("heimdall-");
    }

    function apply(directive, sourceEl, context) {
        context = context || {};
        const targetSelector = (getAttr(directive, "heimdall-content-target") || "").trim();
        if (!targetSelector)
            return mutationError(sourceEl, directive, context, "missing-target", "Mutation target selector is required.");

        const scope = (getAttr(directive, "scope") || "self").trim().toLowerCase();
        if (scope !== "self" && scope !== "subtree" && scope !== "select")
            return mutationError(sourceEl, directive, context, "invalid-scope", `Unsupported mutation scope '${scope}'.`);

        const selector = (getAttr(directive, "selector") || "").trim();
        if (scope === "select" && !selector)
            return mutationError(sourceEl, directive, context, "missing-selector", "Select-scoped mutation requires a selector.");

        let operations;
        let roots;
        let targets;
        try {
            operations = parseOperations(directive);
            roots = resolveRoots(targetSelector, directive.hasAttribute("all"));
            targets = resolveTargets(roots, scope, selector);
        }
        catch (error) {
            return mutationError(sourceEl, directive, context, "invalid-directive", error.message, error);
        }

        if (roots.length === 0)
            return mutationError(sourceEl, directive, context, "target-not-found", `Mutation target '${targetSelector}' was not found.`);

        const detail = {
            origin: context.kind || "action",
            sourceElement: sourceEl || null,
            requestContext: context.requestContext || null,
            directive,
            targetSelector,
            scope,
            selector: scope === "select" ? selector : null,
            allTargets: directive.hasAttribute("all"),
            rootTargets: roots,
            targets,
            operations
        };

        if (emitLifecycle && !emitLifecycle(
            sourceEl,
            "heimdall:mutation-before",
            detail,
            { cancelable: true }
        )) {
            return { applied: false, cancelled: true, targets: [], detail };
        }

        const startedAt = global.performance && typeof global.performance.now === "function"
            ? global.performance.now()
            : Date.now();

        try {
            for (const target of targets) {
                for (const operation of operations)
                    applyOperation(target, operation);
            }
        }
        catch (error) {
            return mutationError(sourceEl, directive, context, "apply-failed", error.message, error);
        }

        const finishedAt = global.performance && typeof global.performance.now === "function"
            ? global.performance.now()
            : Date.now();
        const durationMs = Math.max(0, finishedAt - startedAt);

        if (emitLifecycle) {
            emitLifecycle(sourceEl, "heimdall:mutation-after", {
                ...detail,
                rootCount: roots.length,
                targetCount: targets.length,
                operationCount: operations.length,
                durationMs
            });
        }

        if (durationMs > 16)
            dbg("long mutation batch", { targetSelector, targetCount: targets.length, operationCount: operations.length, durationMs });

        const reconcileTargets = operations.some(requiresBehaviorReconciliation) ? targets : [];
        return { applied: true, cancelled: false, targets, reconcileTargets, detail };
    }

    function reconcile(targets) {
        const seen = new Set();
        for (const target of targets || []) {
            if (!isElement(target) || !target.isConnected || seen.has(target))
                continue;
            seen.add(target);
            try {
                boot(target);
            }
            catch (error) {
                dbg("mutation reconciliation failed", { target, error });
            }
        }
    }

    return {
        apply,
        reconcile
    };
}
