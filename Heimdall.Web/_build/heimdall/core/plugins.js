import { intAttr, truthyAttr } from "./utils.js";

const PLUGIN_API_VERSION = 1;
const validName = /^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$/;
const reservedTriggers = new Set([
    "load", "click", "change", "input", "submit", "keydown", "blur", "hover",
    "visible", "scroll", "document-visible", "online", "target", "swap", "disable"
]);

function abortError() {
    return new DOMException("Plugin operation was cancelled.", "AbortError");
}

function waitFor(promise, signal, timeoutMs) {
    return new Promise((resolve, reject) => {
        let timer;
        const finish = (callback, value) => {
            clearTimeout(timer);
            signal?.removeEventListener("abort", abort);
            callback(value);
        };
        const abort = () => finish(reject, abortError());
        // Observe the operation even when its handler synchronously aborts its own scope.
        Promise.resolve(promise).then(
            value => finish(resolve, value),
            error => finish(reject, error));
        if (signal?.aborted) {
            reject(abortError());
            return;
        }
        signal?.addEventListener("abort", abort, { once: true });
        if (timeoutMs > 0) {
            timer = setTimeout(() => {
                const error = new Error("Plugin operation timed out.");
                error.code = "plugin-timeout";
                finish(reject, error);
            }, timeoutMs);
        }
    });
}

function matchingElements(root, selector) {
    const elements = root?.matches?.(selector) ? [root] : [];
    return elements.concat(Array.from(root?.querySelectorAll?.(selector) || []));
}

function snapshot(value, global) {
    if (typeof global.FormData === "function" && value instanceof global.FormData) {
        const copy = new global.FormData();
        for (const [key, item] of value.entries()) copy.append(key, item);
        return copy;
    }
    return value == null ? value : JSON.parse(JSON.stringify(value));
}

export function createPluginRuntime({ global, getConfig, emitLifecycle, dbg }) {
    const installed = new Map();
    const registries = {
        behaviors: new Map(), triggers: new Map(), payloads: new Map(), commands: new Map()
    };
    let host = {};
    let started = false;
    let registryChanged = () => {};

    function report(pluginId, extension, phase, code, error, extra = {}) {
        const detail = { pluginId, extension, phase, code, error, ...extra };
        emitLifecycle(extra.sourceElement || null, "heimdall:plugin-error", detail);
        dbg("plugin error", detail);
        return error;
    }

    function checkName(value, description) {
        if (typeof value !== "string" || value.length > 128 || !validName.test(value))
            throw new TypeError(`${description} must be a lowercase dotted or hyphenated name.`);
        return value;
    }

    function disposeRecord(definition, element, record) {
        definition.instances.delete(element);
        record.controller.abort();
        try {
            const result = record.dispose?.();
            if (result && typeof result.then === "function")
                Promise.resolve(result).catch(error => report(definition.owner.id, definition.name,
                    "dispose", "dispose-failed", error, { sourceElement: element }));
        } catch (error) {
            report(definition.owner.id, definition.name, "dispose", "dispose-failed", error,
                { sourceElement: element });
        }
    }

    function invokeCleanup(owner, callback) {
        try {
            const result = callback();
            if (result && typeof result.then === "function")
                Promise.resolve(result).catch(error => report(owner.id, null, "dispose", "dispose-failed", error));
        }
        catch (error) { report(owner.id, null, "dispose", "dispose-failed", error); }
    }

    function remove(id) {
        const owner = installed.get(id);
        if (!owner) return false;
        for (const other of installed.values()) {
            if (other.requires.includes(id))
                throw new Error(`Plugin '${id}' is required by '${other.id}'.`);
        }
        installed.delete(id);
        owner.controller.abort();
        for (const definition of owner.definitions) {
            registries[definition.kind].delete(definition.name);
            for (const [element, record] of definition.instances || [])
                disposeRecord(definition, element, record);
        }
        for (const callback of owner.cleanup.reverse()) invokeCleanup(owner, callback);
        registryChanged();
        return true;
    }

    function use(plugin, options) {
        if (!plugin || typeof plugin !== "object" || typeof plugin.install !== "function")
            throw new TypeError("A plugin must provide an install function.");
        const id = checkName(plugin.id, "Plugin id");
        if (id === "heimdall" || id.startsWith("heimdall."))
            throw new Error("The heimdall namespace is reserved.");
        if (plugin.pluginApiVersion !== PLUGIN_API_VERSION)
            throw new Error(`Plugin '${id}' requires an unsupported plugin API version.`);
        if (plugin.version != null && (typeof plugin.version !== "string" || !plugin.version.trim()))
            throw new TypeError("Plugin version must be a non-empty string when provided.");
        const existing = installed.get(id);
        if (existing) {
            if (existing.plugin === plugin && existing.options === options) return;
            throw new Error(`Plugin '${id}' is already installed.`);
        }
        const requires = plugin.requires || [];
        if (!Array.isArray(requires)) throw new TypeError("Plugin requires must be an array of plugin ids.");
        for (const dependency of requires) {
            checkName(dependency, "Dependency id");
            if (!installed.has(dependency))
                throw new Error(`Plugin '${id}' requires '${dependency}' to be installed first.`);
        }
        const owner = {
            id, plugin, options, version: plugin.version || null, requires: [...requires],
            controller: new AbortController(), definitions: [], cleanup: []
        };
        let installing = true;
        const assertInstalling = () => {
            if (!installing) throw new Error("Plugin registrations must be made synchronously during install.");
        };
        function register(kind, descriptor) {
            assertInstalling();
            if (!descriptor || typeof descriptor !== "object")
                throw new TypeError("An extension descriptor is required.");
            const localName = checkName(descriptor.name, "Extension name");
            const name = `${id}.${localName}`;
            if (owner.definitions.some(d => d.kind === kind && d.name === name))
                throw new Error(`Extension '${name}' is already registered.`);
            const definition = { ...descriptor, name, kind, owner, instances: new Map() };
            if (kind === "behaviors") {
                global.document.querySelectorAll(descriptor.selector);
                if (!descriptor.selector || typeof descriptor.mount !== "function")
                    throw new TypeError("A behavior requires a selector and mount function.");
                if (descriptor.observedAttributes != null &&
                    (!Array.isArray(descriptor.observedAttributes) ||
                     descriptor.observedAttributes.some(a => typeof a !== "string" || !a)))
                    throw new TypeError("observedAttributes must contain attribute names.");
                definition.observedAttributes = [...(descriptor.observedAttributes || [])];
            } else if (kind === "triggers") {
                const attribute = descriptor.attribute || `heimdall-content-${name.replace(/\./g, "-")}`;
                if (!/^heimdall-content-[a-z][a-z0-9-]*$/.test(attribute) ||
                    reservedTriggers.has(attribute.substring("heimdall-content-".length)))
                    throw new Error(`Trigger attribute '${attribute}' is invalid or reserved.`);
                for (const other of [...registries.triggers.values(), ...owner.definitions]) {
                    if (other.kind === "triggers" && other.attribute === attribute)
                        throw new Error(`Trigger attribute '${attribute}' is already registered.`);
                }
                const events = descriptor.events || (descriptor.event ? [descriptor.event] : []);
                if (!Array.isArray(events) || events.some(e => typeof e !== "string" || !e || /\s/.test(e)))
                    throw new TypeError("Trigger events must be an array of individual DOM event names.");
                if ((events.length > 0) === (typeof descriptor.subscribe === "function"))
                    throw new TypeError("A trigger requires either DOM events or a subscribe function.");
                definition.attribute = attribute;
                if (descriptor.filter != null && typeof descriptor.filter !== "function")
                    throw new TypeError("A trigger filter must be a function.");
                definition.events = [...events];
            } else if (kind === "payloads") {
                if (typeof descriptor.read !== "function" ||
                    !["trigger", "execution"].includes(descriptor.timing || "trigger"))
                    throw new TypeError("A payload source requires read and trigger/execution timing.");
            } else if (typeof descriptor.handle !== "function") {
                throw new TypeError("A command requires a handle function.");
            }
            if (kind === "commands" && descriptor.validate != null && typeof descriptor.validate !== "function")
                throw new TypeError("A command validator must be a function.");
            owner.definitions.push(definition);
            return name;
        }
        const api = {
            pluginId: id, pluginApiVersion: PLUGIN_API_VERSION,
            signal: owner.controller.signal,
            invoke: (invocationId, payload, options = {}) => {
                if (owner.controller.signal.aborted) return Promise.reject(abortError());
                if (installed.get(id) !== owner)
                    throw new Error("Plugin invocation is available after installation commits.");
                const controller = new AbortController();
                const signals = [owner.controller.signal, options.signal].filter(Boolean);
                const abort = () => controller.abort();
                const unlink = () => {
                    for (const signal of signals) signal.removeEventListener("abort", abort);
                };
                for (const signal of signals) {
                    if (signal.aborted) abort();
                    else signal.addEventListener("abort", abort, { once: true });
                }
                try {
                    return Promise.resolve(host.invoke(invocationId, payload, { ...options, signal: controller.signal })).finally(unlink);
                } catch (error) {
                    unlink();
                    throw error;
                }
            },
            onDispose(callback) {
                assertInstalling();
                if (typeof callback !== "function") throw new TypeError("A cleanup function is required.");
                owner.cleanup.push(callback);
            }
        };
        for (const kind of Object.keys(registries))
            api[kind] = Object.freeze({ register: descriptor => register(kind, descriptor) });
        try {
            const result = plugin.install(Object.freeze(api), options);
            if (result && typeof result.then === "function") {
                Promise.resolve(result).catch(() => {});
                throw new TypeError("Plugin install must be synchronous. Async work belongs in element behaviors.");
            }
            if (result !== undefined && typeof result !== "function")
                throw new TypeError("Plugin install may return a cleanup function or undefined.");
            if (result) owner.cleanup.push(result);
        } catch (error) {
            owner.controller.abort();
            for (const callback of owner.cleanup.reverse()) invokeCleanup(owner, callback);
            report(id, null, "install", "install-failed", error);
            throw error;
        } finally {
            installing = false;
        }
        installed.set(id, owner);
        for (const definition of owner.definitions) registries[definition.kind].set(definition.name, definition);
        registryChanged();
        if (started) host.boot(global.document);
    }

    function signature(definition, element) {
        return JSON.stringify((definition.observedAttributes || []).map(a => element.getAttribute(a)));
    }

    function mountBehavior(definition, element) {
        const previous = definition.instances.get(element);
        const nextSignature = signature(definition, element);
        if (previous) {
            if (previous.signature === nextSignature || previous.pending) return;
            if (previous.update) {
                previous.signature = nextSignature;
                try {
                    const result = previous.update({ element, signal: previous.controller.signal });
                    if (result && typeof result.then === "function") {
                        Promise.resolve(result).catch(() => {});
                        throw new TypeError("Behavior update must be synchronous; remount for asynchronous updates.");
                    }
                } catch (error) {
                    report(definition.owner.id, definition.name, "update", "update-failed", error,
                        { sourceElement: element });
                }
                return;
            }
            disposeRecord(definition, element, previous);
        }
        const record = { controller: new AbortController(), signature: nextSignature, pending: false, ready: null };
        definition.instances.set(element, record);
        function accept(result) {
            if (result != null && typeof result !== "function" && typeof result !== "object")
                throw new TypeError("Behavior mount must return cleanup, a behavior instance, or undefined.");
            record.dispose = typeof result === "function" ? result : result?.dispose?.bind(result);
            record.update = typeof result === "object" ? result?.update?.bind(result) : null;
            if (record.controller.signal.aborted) return record.dispose?.();
            record.pending = false;
            if (!record.controller.signal.aborted) mountBehavior(definition, element);
        }
        try {
            const result = definition.mount(element, {
                element, pluginId: definition.owner.id, options: definition.owner.options,
                signal: record.controller.signal
            });
            if (result && typeof result.then === "function") {
                record.pending = true;
                // Accept eventual cleanup even if cancellation wins the readiness race.
                const mounting = Promise.resolve(result).then(accept);
                record.ready = waitFor(mounting, record.controller.signal,
                    getConfig().pluginMountTimeoutMs ?? 10000).then(() => {
                    record.ready = null;
                }).catch(error => {
                    record.pending = false;
                    record.controller.abort();
                    if (error?.name !== "AbortError")
                        report(definition.owner.id, definition.name, "mount", error?.code || "mount-failed", error,
                            { sourceElement: element });
                    throw error;
                });
                record.ready.catch(() => {});
            } else accept(result);
        } catch (error) {
            record.controller.abort();
            record.ready = Promise.reject(error);
            record.ready.catch(() => {});
            report(definition.owner.id, definition.name, "mount", "mount-failed", error,
                { sourceElement: element });
        }
    }

    function mountTrigger(definition, element) {
        if (definition.instances.has(element)) return;
        const record = { controller: new AbortController() };
        definition.instances.set(element, record);
        const emit = async (event = null, overrides = {}) => {
            if (record.controller.signal.aborted || !element.isConnected) return Promise.resolve(null);
            if (event?.defaultPrevented) return Promise.resolve(null);
            const eventTarget = event?.target || element;
            if (host.resolveTriggerElement(eventTarget, definition.attribute, definition.name) !== element)
                return Promise.resolve(null);
            if (definition.filter && !definition.filter({ element, event })) return Promise.resolve(null);
            const submitted = { ...overrides };
            if (Object.prototype.hasOwnProperty.call(submitted, "payload"))
                submitted.payload = snapshot(submitted.payload, global);
            if (truthyAttr(element, "heimdall-prevent-default", !!definition.preventDefault)) event?.preventDefault?.();
            const invoke = () => {
                if (record.controller.signal.aborted || !element.isConnected) return;
                return host.runInvocationFromElement(element, element.getAttribute(definition.attribute),
                    definition.name, { ...submitted, event });
            };
            const delay = intAttr(element, "heimdall-debounce", Number(definition.debounceMs || 0));
            if (delay > 0) {
                record.cancelDebounce?.();
                return new Promise((resolve, reject) => {
                    const cancel = host.scheduleDebounced(element, definition.name, delay, () => {
                        record.cancelDebounce = null;
                        Promise.resolve(invoke()).then(resolve, reject);
                    });
                    record.cancelDebounce = () => { cancel(); resolve(null); };
                });
            }
            return Promise.resolve(invoke());
        };
        try {
            for (const event of definition.events) {
                element.addEventListener(event, e => {
                    emit(e).catch(error => report(definition.owner.id, definition.name,
                        "trigger", "trigger-failed", error, { sourceElement: element }));
                }, { signal: record.controller.signal });
            }
            const cleanup = definition.subscribe?.({ element, emit, signal: record.controller.signal });
            if (cleanup && typeof cleanup.then === "function") Promise.resolve(cleanup).catch(() => {});
            if (cleanup != null && typeof cleanup !== "function")
                throw new TypeError("Trigger subscribe must return cleanup or undefined.");
            record.dispose = () => { record.cancelDebounce?.(); cleanup?.(); };
        } catch (error) {
            record.controller.abort();
            record.cancelDebounce?.();
            report(definition.owner.id, definition.name, "subscribe", "subscribe-failed", error,
                { sourceElement: element });
        }
    }

    function boot(root) {
        if (!root || (root.nodeType === 1 && !root.isConnected)) return;
        for (const kind of ["behaviors", "triggers"]) {
            for (const definition of registries[kind].values()) {
                const selector = kind === "behaviors" ? definition.selector : `[${definition.attribute}]`;
                for (const [element, record] of definition.instances) {
                    if (!element.isConnected || !element.matches(selector))
                        disposeRecord(definition, element, record);
                }
                for (const element of matchingElements(root, selector)) {
                    if (kind === "behaviors") mountBehavior(definition, element);
                    else mountTrigger(definition, element);
                }
            }
        }
    }

    function dispose(root, preserved = []) {
        for (const kind of ["triggers", "behaviors"]) {
            for (const definition of registries[kind].values()) {
                for (const [element, record] of definition.instances) {
                    if ((root === element || root?.contains?.(element)) &&
                        !preserved.some(node => node === element || node.contains?.(element)))
                        disposeRecord(definition, element, record);
                }
            }
        }
    }

    function ready(root, subtree = false) {
        const promises = [];
        for (const definition of registries.behaviors.values()) {
            for (const [element, record] of definition.instances) {
                if (record.ready && (element === root || element.contains(root) || (subtree && root?.contains?.(element)))) {
                    promises.push(record.ready.catch(error => {
                        const replacement = definition.instances.get(element);
                        if (error?.name === "AbortError" && replacement && replacement !== record) return;
                        throw error;
                    }));
                }
            }
        }
        // A watched-options change can replace a pending mount. Follow its replacement
        // before declaring the target ready for payload reads or commands.
        return promises.length ? Promise.all(promises).then(() => ready(root, subtree)) : null;
    }

    function readPayload(name, element) {
        const definition = registries.payloads.get(name);
        if (!definition) throw report(null, name, "payload", "unknown-payload", new Error(`Unknown plugin payload '${name}'.`), { sourceElement: element });
        const read = () => {
            try {
                const value = definition.read({ element, options: definition.owner.options });
                if (value && typeof value.then === "function") {
                    Promise.resolve(value).catch(() => {});
                    throw new TypeError("Payload readers must be synchronous. Initialize asynchronous data in a behavior.");
                }
                return snapshot(value, global);
            } catch (error) {
                throw report(definition.owner.id, name, "payload", "payload-failed", error, { sourceElement: element });
            }
        };
        return {
            value: read(),
            binding: definition.timing === "execution" ? {
                kind: "plugin", resolve: () => element.isConnected && registries.payloads.get(name) === definition
                    ? { available: true, value: read() } : { available: false, value: null }
            } : null
        };
    }

    function prepareCommands(html) {
        if (!/<heimdall-command[\s>]/i.test(html || "")) return { html, before: [], after: [] };
        const template = global.document.createElement("template");
        template.innerHTML = html;
        const redirect = template.content.querySelector("redirect");
        const suppress = !!(redirect && (redirect.getAttribute("url")?.trim() || redirect.textContent.trim()));
        const before = [], after = [];
        let limitReported = false;
        for (const node of Array.from(template.content.querySelectorAll("heimdall-command"))) {
            if (before.length + after.length >= (getConfig().pluginCommandMaxCount ?? 256)) {
                if (!limitReported) report(null, null, "command", "command-limit", new Error("Too many plugin commands in a response."));
                limitReported = true;
            } else if (!suppress) {
                const command = {
                    name: node.getAttribute("name"), argsJson: node.getAttribute("args") || "{}",
                    target: node.getAttribute("target"), all: node.getAttribute("all") === "true",
                    timing: node.getAttribute("timing") || "after"
                };
                (command.timing === "before" ? before : after).push(command);
            }
            node.remove();
        }
        stripCommands(template.content);
        const wrapper = global.document.createElement("div");
        wrapper.append(template.content);
        return { html: wrapper.innerHTML, before, after };
    }

    function stripCommands(root) {
        for (const node of Array.from(root?.querySelectorAll?.("heimdall-command") || [])) node.remove();
        for (const template of Array.from(root?.querySelectorAll?.("template") || [])) stripCommands(template.content);
    }

    async function runCommands(commands, context) {
        for (const command of commands) {
            if (context.isCurrent && !context.isCurrent()) return false;
            const definition = registries.commands.get(command.name);
            if (!definition) {
                report(null, command.name, "command", "unknown-command", new Error(`Unknown plugin command '${command.name}'.`), context);
                continue;
            }
            const controller = new AbortController();
            const signals = [context.signal, definition.owner.controller.signal].filter(Boolean);
            const abort = () => controller.abort();
            for (const signal of signals) {
                if (signal.aborted) abort();
                else signal.addEventListener("abort", abort, { once: true });
            }
            try {
                if (!["before", "after"].includes(command.timing)) throw new Error("Invalid command timing.");
                if (command.argsJson.length > (getConfig().pluginCommandMaxArgsChars ?? 65536))
                    throw new Error("Plugin command arguments exceed the configured limit.");
                const args = JSON.parse(command.argsJson);
                if (definition.validate && definition.validate(args) !== true) throw new Error("Invalid plugin command arguments.");
                const targets = command.target
                    ? (command.all ? Array.from(global.document.querySelectorAll(command.target))
                        : [global.document.querySelector(command.target)].filter(Boolean))
                    : [null];
                if (!targets.length) throw new Error(`Plugin command target '${command.target}' was not found.`);
                for (const element of targets) {
                    if (controller.signal.aborted || (context.isCurrent && !context.isCurrent())) return false;
                    if (element) {
                        boot(element);
                        const pending = ready(element, true);
                        if (pending) await waitFor(pending, controller.signal, getConfig().pluginMountTimeoutMs ?? 10000);
                    }
                    if (controller.signal.aborted || (context.isCurrent && !context.isCurrent())) return false;
                    if (element && !element.isConnected) throw new Error("Plugin command target was disconnected.");
                    await waitFor(definition.handle({
                        element, args, signal: controller.signal, pluginId: definition.owner.id,
                        invocationId: context.invocationId || null, sourceElement: context.sourceElement || null,
                        origin: context.origin || "invocation", timing: command.timing
                    }), controller.signal, getConfig().pluginCommandTimeoutMs ?? 10000);
                }
            } catch (error) {
                if (error?.name !== "AbortError")
                    report(definition.owner.id, command.name, "command", error?.code || "command-failed", error,
                        { invocationId: context.invocationId || null, sourceElement: context.sourceElement || null });
            } finally {
                controller.abort();
                for (const signal of signals) signal.removeEventListener("abort", abort);
            }
        }
        return !context.isCurrent || context.isCurrent();
    }

    return {
        pluginApiVersion: PLUGIN_API_VERSION, use, remove, boot, dispose, ready, readPayload,
        prepareCommands, stripCommands, runCommands,
        configure(value) { host = value; },
        start() { started = true; },
        hasPlugins: () => installed.size > 0,
        onRegistryChanged(callback) { registryChanged = callback; },
        observe(mutations) {
            if (!installed.size) return;
            const roots = new Set(mutations.map(m => m.target).filter(n => n.isConnected));
            for (const root of roots) {
                if (!Array.from(roots).some(other => other !== root && other.contains?.(root))) boot(root);
            }
        },
        list: () => Array.from(installed.values(), owner => Object.freeze({
            id: owner.id, version: owner.version, pluginApiVersion: PLUGIN_API_VERSION,
            requires: Object.freeze([...owner.requires])
        }))
    };
}
