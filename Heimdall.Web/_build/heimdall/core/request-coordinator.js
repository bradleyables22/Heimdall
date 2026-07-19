const VALID_SYNC_STRATEGIES = new Set([
    "parallel",
    "replace",
    "drop",
    "queue-latest"
]);

export function createRequestCoordinator({ global, dbg }) {
    const elementStates = new WeakMap();
    const groupStates = new Map();
    const recordsByContext = new WeakMap();

    function normalizeStrategy(value, fallback) {
        const normalized = String(value || "").trim().toLowerCase();
        if (VALID_SYNC_STRATEGIES.has(normalized))
            return normalized;

        const normalizedFallback = String(fallback || "parallel").trim().toLowerCase();
        if (VALID_SYNC_STRATEGIES.has(normalizedFallback))
            return normalizedFallback;

        return "parallel";
    }

    function cancellationResult(context, reason) {
        return {
            ok: false,
            status: 0,
            error: null,
            response: null,
            html: null,
            ms: Math.max(0, performance.now() - context.startedAt),
            cancelled: true,
            cancelReason: reason || "cancelled",
            requestId: context.requestId
        };
    }

    function getState(record) {
        if (record.group) {
            let state = groupStates.get(record.group);
            if (!state) {
                state = { active: null, queued: null, generation: 0, group: record.group };
                groupStates.set(record.group, state);
            }
            return state;
        }

        if (record.context.sourceElement) {
            let state = elementStates.get(record.context.sourceElement);
            if (!state) {
                state = { active: null, queued: null, generation: 0, group: null };
                elementStates.set(record.context.sourceElement, state);
            }
            return state;
        }

        return null;
    }

    function cleanupState(state) {
        if (!state || state.active || state.queued)
            return;

        if (state.group)
            groupStates.delete(state.group);
    }

    function abortController(record) {
        if (!record.controller)
            return;

        try {
            record.controller.abort(record.cancelReason);
        } catch {
            try {
                record.controller.abort();
            } catch {
                // ignore
            }
        }
    }

    function settleQueuedCancellation(record, reason) {
        if (!record || record.settled)
            return;

        record.cancelled = true;
        record.cancelReason = reason || "cancelled";
        record.context.cancelled = true;
        record.context.cancelReason = record.cancelReason;
        cleanupRecord(record);
        record.settled = true;
        record.resolve(cancellationResult(record.context, record.cancelReason));
    }

    function cancelRecord(record, reason) {
        if (!record || record.settled || record.cancelled)
            return false;

        record.cancelled = true;
        record.cancelReason = reason || "cancelled";
        record.context.cancelled = true;
        record.context.cancelReason = record.cancelReason;

        if (record.started) {
            abortController(record);
        } else {
            if (record.state && record.state.queued === record)
                record.state.queued = null;
            settleQueuedCancellation(record, record.cancelReason);
            cleanupState(record.state);
        }

        return true;
    }

    function linkExternalSignal(record) {
        const signal = record.context.externalSignal;
        if (!signal || typeof signal.addEventListener !== "function")
            return;

        if (signal.aborted) {
            cancelRecord(record, "external-signal");
            return;
        }

        const handler = () => cancelRecord(record, "external-signal");
        signal.addEventListener("abort", handler, { once: true });
        record.removeExternalAbort = () => {
            try {
                signal.removeEventListener("abort", handler);
            } catch {
                // ignore
            }
        };
    }

    function startTimeout(record) {
        const timeoutMs = Number(record.context.timeoutMs || 0);
        if (!Number.isFinite(timeoutMs) || timeoutMs <= 0)
            return;

        record.timeoutId = global.setTimeout(() => {
            record.timedOut = true;
            record.context.timedOut = true;
            cancelRecord(record, "timeout");
        }, timeoutMs);
    }

    function cleanupRecord(record) {
        if (record.timeoutId != null) {
            global.clearTimeout(record.timeoutId);
            record.timeoutId = null;
        }

        if (record.removeExternalAbort) {
            record.removeExternalAbort();
            record.removeExternalAbort = null;
        }
    }

    async function startRecord(record) {
        if (record.settled)
            return;

        const state = record.state;
        if (state) {
            state.active = record;
            record.generation = ++state.generation;
        }

        record.started = true;
        record.context.startedExecutionAt = performance.now();
        startTimeout(record);

        let result;
        try {
            if (record.cancelled) {
                result = cancellationResult(record.context, record.cancelReason);
            } else {
                result = await record.execute();
            }
        } catch (error) {
            cleanupRecord(record);
            record.settled = true;
            record.reject(error);

            if (state && state.active === record) {
                state.active = null;
                const queued = state.queued;
                state.queued = null;
                if (queued)
                    startRecord(queued);
                else
                    cleanupState(state);
            }
            return;
        }

        cleanupRecord(record);
        record.settled = true;
        record.resolve(result);

        if (state && state.active === record) {
            state.active = null;
            const queued = state.queued;
            state.queued = null;
            if (queued)
                startRecord(queued);
            else
                cleanupState(state);
        }
    }

    function run(context, execute) {
        return new Promise((resolve, reject) => {
            const Controller = global.AbortController || globalThis.AbortController;
            const controller = typeof Controller === "function" ? new Controller() : null;
            const record = {
                context,
                execute,
                resolve,
                reject,
                controller,
                state: null,
                group: context.syncGroup ? String(context.syncGroup).trim() : null,
                generation: 0,
                started: false,
                settled: false,
                cancelled: false,
                cancelReason: null,
                timedOut: false,
                timeoutId: null,
                removeExternalAbort: null
            };

            context.controller = controller;
            context.signal = controller ? controller.signal : null;
            recordsByContext.set(context, record);
            linkExternalSignal(record);

            if (record.cancelled) {
                settleQueuedCancellation(record, record.cancelReason);
                return;
            }

            const strategy = normalizeStrategy(context.sync, "parallel");
            context.sync = strategy;

            if (strategy === "parallel") {
                startRecord(record);
                return;
            }

            const state = getState(record);
            record.state = state;

            if (!state || !state.active) {
                startRecord(record);
                return;
            }

            switch (strategy) {
                case "replace":
                    cancelRecord(state.active, "replaced");
                    if (state.queued)
                        cancelRecord(state.queued, "queue-replaced");
                    state.queued = null;
                    startRecord(record);
                    break;
                case "drop":
                    settleQueuedCancellation(record, "dropped");
                    break;
                case "queue-latest":
                    if (state.queued)
                        cancelRecord(state.queued, "queue-replaced");
                    state.queued = record;
                    break;
                default:
                    dbg("unknown request synchronization strategy; using parallel", { strategy });
                    startRecord(record);
                    break;
            }
        });
    }

    function cancel(context, reason) {
        return cancelRecord(recordsByContext.get(context), reason);
    }

    function isCurrent(context) {
        const record = recordsByContext.get(context);
        if (!record || record.cancelled)
            return false;

        if (!record.state)
            return true;

        return record.state.active === record && record.state.generation === record.generation;
    }

    function getCancellationResult(context) {
        const record = recordsByContext.get(context);
        const reason = record && record.cancelReason
            ? record.cancelReason
            : (context.cancelReason || "cancelled");
        return cancellationResult(context, reason);
    }

    return {
        cancel,
        getCancellationResult,
        isCurrent,
        normalizeStrategy,
        run
    };
}
