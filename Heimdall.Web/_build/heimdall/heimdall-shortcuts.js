// Optional, automatically installed attribute shortcuts. Load after the core runtime.
(function (global) {
    if (!global.Heimdall)
        throw new Error("Load the Heimdall runtime before heimdall-shortcuts.js.");

    global.Heimdall.use({
        id: "hd.shortcuts",
        pluginApiVersion: 1,
        version: "1.0.0",
        install(api) {
            const aliases = {};
            for (const definition of api.attributes.list()) {
                if (definition.owner !== "heimdall") continue;
                aliases[definition.name] = definition.name
                    .replace(/^data-heimdall-state/, "data-hd-state")
                    .replace(/^heimdall-content-/, "hd-")
                    .replace(/^heimdall-payload-from$/, "hd-from")
                    .replace(/^heimdall-payload-ref$/, "hd-ref")
                    .replace(/^heimdall-/, "hd-");
            }
            api.attributes.alias(aliases);
        }
    });
})(globalThis);
