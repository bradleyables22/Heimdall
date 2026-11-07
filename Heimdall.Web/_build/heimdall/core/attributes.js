const builtins = [
    ...["click", "submit", "input", "change", "keydown", "blur", "hover", "load",
        "visible", "scroll", "document-visible", "online"].map(name => [`heimdall-content-${name}`, "trigger"]),
    ...["target", "swap", "disable"].map(name => [`heimdall-content-${name}`, "content"]),
    ...["payload", "payload-from", "payload-ref"].map(name => [`heimdall-${name}`, "payload"]),
    ...["prevent-default", "ignore", "scope", "sync", "sync-group", "debounce", "key",
        "hover-delay", "visible-once", "scroll-threshold", "poll"].map(name => [`heimdall-${name}`, "modifier"]),
    ...["sse", "sse-topic", "sse-target", "sse-swap", "sse-event", "sse-disable"].map(name => [`heimdall-${name}`, "sse"]),
    ["heimdall-time", "time"], ["heimdall-time-format", "time"],
    ["data-heimdall-state", "state"], ["data-heimdall-state-*", "state"]
];

const nativeNames = new Set(["id", "class", "style", "name", "value", "type", "disabled",
    "hidden", "checked", "selected", "multiple", "required", "readonly", "lang", "dir",
    "role", "command", "commandfor", "action", "method", "enctype", "form", "href", "src"]);

function validateName(name, prefix) {
    const value = prefix ? name?.slice(0, -1) : name;
    if (typeof name !== "string" || (prefix && !name.endsWith("-*")) ||
        !/^[a-z][a-z0-9_-]*$/.test(value || "") || /^on[a-z]+$/.test(name) ||
        name.startsWith("aria-") || nativeNames.has(name))
        throw new TypeError(`Invalid or browser-owned attribute name '${name}'.`);
}

function overlaps(a, b) {
    const ap = a.endsWith("*"), bp = b.endsWith("*");
    if (ap && bp) return a.slice(0, -1).startsWith(b.slice(0, -1)) || b.slice(0, -1).startsWith(a.slice(0, -1));
    if (ap) return b.startsWith(a.slice(0, -1));
    if (bp) return a.startsWith(b.slice(0, -1));
    return a === b;
}

// Originals are immutable identities. Aliases add spellings, never DOM rewrites.
export function createAttributeRegistry() {
    let definitions = new Map(builtins.map(([name, category]) => [name,
        Object.freeze({ name, category, kind: name.endsWith("*") ? "prefix" : "attribute", owner: "heimdall" })]));
    let layers = new Map();
    let depth = 0, dirty = false;
    let recognizedPatterns = null;
    const listeners = new Set(), cache = new Map();

    function effective(stack) {
        for (let index = (stack?.length || 0) - 1; index >= 0; index--) {
            const record = stack[index];
            if (definitions.get(record.definition.name) === record.definition) return record;
        }
        return null;
    }

    function changed() {
        cache.clear();
        recognizedPatterns = null;
        dirty = true;
        if (!depth) {
            dirty = false;
            for (const listener of listeners) listener();
        }
    }

    function transaction(callback) {
        const previousDefinitions = new Map(definitions);
        const previousLayers = new Map(Array.from(layers, ([name, stack]) => [name, [...stack]]));
        const previousDirty = dirty;
        depth++;
        try { return callback(); }
        catch (error) {
            definitions = previousDefinitions;
            layers = previousLayers;
            dirty = previousDirty;
            cache.clear();
            recognizedPatterns = null;
            throw error;
        } finally {
            depth--;
            if (!depth && dirty) changed();
        }
    }

    function aliasesFor(definition) {
        return Array.from(layers, ([name, stack]) => effective(stack)?.definition === definition ? name : null).filter(Boolean);
    }

    function list() {
        return Object.freeze(Array.from(definitions.values(), definition => Object.freeze({
            ...definition, aliases: Object.freeze(aliasesFor(definition))
        })));
    }

    function alias(mappings, options = {}, owner = null) {
        if (!mappings || typeof mappings !== "object" || Array.isArray(mappings))
            throw new TypeError("Attribute aliases must be a map of original names to aliases.");
        const records = [];
        transaction(() => {
            for (const [original, values] of Object.entries(mappings)) {
                const definition = definitions.get(original);
                if (!definition) throw new Error(`Unknown original attribute '${original}'.`);
                const names = typeof values === "string" ? [values] : values;
                if (!Array.isArray(names)) throw new TypeError(`Aliases for '${original}' must be a string or array.`);
                for (const name of new Set(names)) {
                    validateName(name, definition.kind === "prefix");
                    if (name === original) continue;
                    for (const canonical of definitions.keys()) {
                        if (overlaps(name, canonical)) throw new Error(`Alias '${name}' conflicts with original attribute '${canonical}'.`);
                    }
                    for (const [other, stack] of layers) {
                        const current = effective(stack);
                        if (!current || !overlaps(name, other)) continue;
                        if (name !== other || (current.definition !== definition && options.replace !== true))
                            throw new Error(`Attribute alias '${name}' is already registered for '${current.definition.name}'.`);
                    }
                    const record = { definition, owner };
                    const stack = layers.get(name) || [];
                    stack.push(record);
                    layers.set(name, stack);
                    records.push([name, record]);
                }
            }
            if (records.length) changed();
        });
        let removed = false;
        return () => {
            if (removed) return;
            removed = true;
            let didRemove = false;
            for (const [name, record] of records) {
                const stack = layers.get(name);
                const index = stack?.indexOf(record) ?? -1;
                if (index < 0) continue;
                stack.splice(index, 1);
                if (!stack.length) layers.delete(name);
                didRemove = true;
            }
            if (didRemove) changed();
        };
    }

    function register(name, owner, aliases = []) {
        transaction(() => {
            validateName(name, false);
            if (definitions.has(name)) throw new Error(`Original attribute '${name}' is already registered.`);
            for (const canonical of definitions.keys()) {
                if (overlaps(name, canonical)) throw new Error(`Attribute '${name}' conflicts with '${canonical}'.`);
            }
            for (const [other, stack] of layers) {
                if (effective(stack) && overlaps(name, other)) throw new Error(`Attribute '${name}' conflicts with alias '${other}'.`);
            }
            definitions.set(name, Object.freeze({ name, category: "plugin-trigger", kind: "attribute", owner: owner.id }));
            alias({ [name]: aliases }, {}, owner);
            owner.attributes.add(name);
            changed();
        });
    }

    function removeOwner(owner) {
        let removed = false;
        for (const [name, definition] of definitions) {
            if (definition.owner === owner.id && owner.attributes?.has(name)) {
                definitions.delete(name);
                removed = true;
            }
        }
        for (const [name, stack] of layers) {
            const remaining = stack.filter(record => record.owner !== owner && definitions.get(record.definition.name) === record.definition);
            if (remaining.length === stack.length) continue;
            if (remaining.length) layers.set(name, remaining);
            else layers.delete(name);
            removed = true;
        }
        if (removed) changed();
    }

    function names(original) {
        if (cache.has(original)) return cache.get(original);
        let definition = definitions.get(original), suffix = "";
        if (!definition) {
            definition = Array.from(definitions.values()).find(entry => entry.kind === "prefix" && original.startsWith(entry.name.slice(0, -1)));
            if (definition) suffix = original.slice(definition.name.length - 1);
        }
        const concretePrefix = definition?.kind === "prefix" && original !== definition.name;
        const result = definition
            ? [definition.name, ...aliasesFor(definition)].map(name => concretePrefix ? name.slice(0, -1) + suffix : name)
            : [original];
        const frozen = Object.freeze(result);
        if (cache.size >= 1024) cache.clear();
        cache.set(original, frozen);
        return frozen;
    }

    function get(element, original) {
        for (const name of names(original)) {
            const value = element.getAttribute(name);
            if (value != null) return value;
        }
        return null;
    }

    function has(element, original) { return names(original).some(name => element.hasAttribute(name)); }
    function selector(original) {
        if (original.endsWith("*")) throw new TypeError("Attribute prefixes require a concrete suffix for selectors.");
        return names(original).map(name => `[${CSS.escape(name)}]`).join(",");
    }
    function truthy(element, original, fallback) {
        const value = get(element, original);
        if (value == null) return !!fallback;
        const normalized = value.toLowerCase().trim();
        if (["", "true", "1", "yes"].includes(normalized)) return true;
        if (["false", "0", "no"].includes(normalized)) return false;
        return !!fallback;
    }
    function int(element, original, fallback) {
        const value = get(element, original);
        const number = value == null || value === "" ? NaN : parseInt(value, 10);
        return Number.isFinite(number) ? number : fallback;
    }
    function recognizes(name) {
        recognizedPatterns ||= [...definitions.keys(), ...Array.from(layers.keys()).filter(alias => effective(layers.get(alias)))];
        return recognizedPatterns.some(pattern => pattern.endsWith("*") ? name.startsWith(pattern.slice(0, -1)) : name === pattern);
    }

    const publicApi = Object.freeze({ list, alias, names, get, has, selector });
    return { publicApi, list, alias, register, removeOwner, transaction, names, get, has, selector, truthy, int, recognizes,
        onChange(callback) { listeners.add(callback); return () => listeners.delete(callback); },
        scoped(owner, assertInstalling) {
            return Object.freeze({ ...publicApi, alias(mappings, options) {
                assertInstalling();
                return alias(mappings, options, owner);
            } });
        }
    };
}
