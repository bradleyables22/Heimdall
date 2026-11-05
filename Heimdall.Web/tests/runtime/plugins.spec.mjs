import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { installFakeServer, actionFetches, getFetches } from "../helpers/runtime-page.mjs";

async function testRegistration(page) {
  await installFakeServer(page);
  const result = await page.evaluate(async () => {
    const failures = [], errors = [];
    document.addEventListener("heimdall:plugin-error", e => errors.push(e.detail.code));
    const attempt = callback => { try { callback(); } catch (e) { failures.push(e.message); } };
    let cleanup = 0;
    attempt(() => Heimdall.use({ id: "test.bad", pluginApiVersion: 99, install() {} }));
    attempt(() => Heimdall.use({ id: "test.consumer", pluginApiVersion: 1, requires: ["test.missing"], install() {} }));
    attempt(() => Heimdall.use({ id: "test.rollback", pluginApiVersion: 1, install(api) {
      api.onDispose(() => cleanup++);
      api.commands.register({ name: "run", handle() { throw new Error("leaked"); } });
      throw new Error("install failed");
    } }));
    const plugin = { id: "test.rollback", version: "1.0.0", pluginApiVersion: 1, install(api) {
      api.commands.register({ name: "run", handle() { window.__commandRan = true; } });
    } };
    Heimdall.use(plugin);
    Heimdall.use(plugin);
    attempt(() => Heimdall.use({ ...plugin }));
    attempt(() => Heimdall.use({ id: "test.reserved", pluginApiVersion: 1, install(api) {
      api.triggers.register({ name: "run", attribute: "heimdall-content-click", events: ["commit"] });
    } }));
    Heimdall.use({ id: "test.dependent", pluginApiVersion: 1, requires: [plugin.id], install() {} });
    attempt(() => Heimdall.plugins.remove(plugin.id));
    await Heimdall.invoke("plugin.test", {}, { swap: "none" });
    return { cleanup, failures, errors, list: Heimdall.plugins.list(), version: Heimdall.pluginApiVersion };
  });
  assert.equal(result.version, 1);
  assert.equal(result.cleanup, 1);
  assert.equal(result.failures.length, 6);
  assert.deepEqual(result.list.map(p => p.id), ["test.rollback", "test.dependent"]);
  assert.equal(result.list[0].version, "1.0.0");
  assert.ok(result.errors.includes("install-failed"));
}

async function testLifetimes(page) {
  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="a" data-widget data-value="one"><div id="b" data-widget></div></div><div id="other"></div>';
    const mounts = [], disposals = [], updates = [];
    Heimdall.use({ id: "test.lifecycle", pluginApiVersion: 1, install(api) {
      api.behaviors.register({ name: "widget", selector: "[data-widget]", observedAttributes: ["data-value"],
        mount(element) {
          mounts.push(element.id);
          return { dispose() { disposals.push(element.id); }, update() { updates.push(element.id); } };
        }
      });
    } });
    const tick = () => new Promise(r => setTimeout(r, 0));
    Heimdall.boot(document);
    Heimdall.boot(document.querySelector("#a"));
    document.querySelector("#other").append(document.querySelector("#a"));
    await tick();
    document.querySelector("#a").setAttribute("data-value", "two");
    await tick();
    document.querySelector("#b").removeAttribute("data-widget");
    await tick();
    document.querySelector("#a").remove();
    await tick();
    return { mounts, disposals, updates };
  });
  assert.deepEqual(state.mounts, ["a", "b"]);
  assert.deepEqual(state.updates, ["a"]);
  assert.deepEqual(state.disposals, ["b", "a"]);
}

async function testSwapLifetimes(page) {
  await installFakeServer(page, { actionResponses: [
    { body: '<div id="first" data-widget></div><div id="second" data-widget></div>' },
    { body: '<p>cancelled</p>' }
  ] });
  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="host"><span id="old" data-widget></span></div>';
    const mounts = [], disposals = [];
    Heimdall.config.observeDom = false;
    Heimdall._observer.disconnect();
    Heimdall.use({ id: "test.swap", pluginApiVersion: 1, install(api) {
      api.behaviors.register({ name: "widget", selector: "[data-widget]", mount(el) {
        mounts.push(el.id); return () => disposals.push(el.id);
      } });
    } });
    await Heimdall.invoke("plugin.swap", {}, { target: "#host" });
    document.addEventListener("heimdall:swap-before", e => e.preventDefault(), { once: true });
    await Heimdall.invoke("plugin.cancel-swap", {}, { target: "#host" });
    return { mounts, disposals, second: !!document.querySelector("#second") };
  });
  assert.deepEqual(state.mounts, ["old", "first", "second"]);
  assert.deepEqual(state.disposals, ["old"]);
  assert.equal(state.second, true);
}

async function testCustomTrigger(page) {
  await installFakeServer(page);
  await page.evaluate(async () => {
    document.body.innerHTML = '<div id="outer" heimdall-content-test-events-commit="outer"><button id="inner" heimdall-content-test-events-commit="inner" heimdall-content-swap="none" heimdall-debounce="10" heimdall-payload-from="plugin:test.events.value"></button></div>';
    Heimdall.use({ id: "test.events", pluginApiVersion: 1, install(api, options) {
      api.triggers.register({ name: "commit", events: ["editor-commit"] });
      api.payloads.register({ name: "value", read: ({ element }) => ({ id: element.id, mode: options.mode }) });
    } }, { mode: "production" });
    const el = document.querySelector("#inner");
    el.dispatchEvent(new Event("editor-commit", { bubbles: true }));
    el.dispatchEvent(new Event("editor-commit", { bubbles: true }));
    await new Promise(r => setTimeout(r, 50));
    el.setAttribute("heimdall-scope", "self");
    el.innerHTML = '<span id="child"></span>';
    document.querySelector("#child").dispatchEvent(new Event("editor-commit", { bubbles: true }));
    await new Promise(r => setTimeout(r, 25));
  });
  const requests = actionFetches(await getFetches(page));
  assert.equal(requests.length, 1);
  assert.equal(requests[0].headers["x-heimdall-content-invocation"], "inner");
  assert.deepEqual(requests[0].jsonBody, { id: "inner", mode: "production" });
  assert.equal(requests[0].headers.requestverificationtoken, "csrf-token");
}

async function testSubscriptionAndRemoval(page) {
  await installFakeServer(page);
  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<button id="source" heimdall-content-test-subscription-commit="plugin.commit" heimdall-content-swap="none" heimdall-debounce="10"></button>';
    let send, cleaned = 0, signal;
    Heimdall.use({ id: "test.subscription", pluginApiVersion: 1, install(api) {
      api.triggers.register({ name: "commit", subscribe(context) {
        send = context.emit; signal = context.signal;
        return () => cleaned++;
      } });
    } });
    await send(null, { payload: { answer: 42 } });
    const pending = send(null, { payload: { answer: 99 } });
    Heimdall.plugins.remove("test.subscription");
    await pending;
    await send(null, { payload: { answer: 99 } });
    return { cleaned, aborted: signal.aborted };
  });
  assert.deepEqual(state, { cleaned: 1, aborted: true });
  const requests = actionFetches(await getFetches(page));
  assert.equal(requests.length, 1);
  assert.deepEqual(requests[0].jsonBody, { answer: 42 });
}

async function testBuiltinPayload(page) {
  await installFakeServer(page);
  await page.evaluate(async () => {
    document.body.innerHTML = '<form id="form" heimdall-content-submit="plugin.save" heimdall-content-swap="none" heimdall-payload-from="plugin:test.form.document"></form>';
    Heimdall.use({ id: "test.form", pluginApiVersion: 1, install(api) {
      api.payloads.register({ name: "document", read: () => ({ document: "server-owned" }) });
    } });
    const done = new Promise(resolve => document.addEventListener("heimdall:request-finally", resolve, { once: true }));
    document.querySelector("#form").dispatchEvent(new Event("submit", { bubbles: true, cancelable: true }));
    await done;
  });
  assert.deepEqual(actionFetches(await getFetches(page))[0].jsonBody, { document: "server-owned" });
}

async function testCommandReadiness(page) {
  await installFakeServer(page, { actionResponses: [{ body:
    '<heimdall-command name="test.commands.before" timing="before"></heimdall-command>' +
    '<div id="new-a" data-widget></div><div id="new-b" data-widget></div>' +
    '<heimdall-command name="test.commands.after" target="[data-widget]" all="true" args="{&quot;value&quot;:42}"></heimdall-command>'
  }] });
  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="host">old</div>';
    const order = [], ready = new WeakSet();
    Heimdall.use({ id: "test.commands", pluginApiVersion: 1, install(api) {
      api.behaviors.register({ name: "widget", selector: "[data-widget]", async mount(el) {
        await new Promise(r => setTimeout(r, 15));
        ready.add(el); return () => ready.delete(el);
      } });
      api.commands.register({ name: "before", handle() { order.push(document.querySelector("#host").textContent); } });
      api.commands.register({ name: "after", validate: args => typeof args.value === "number", handle({ element, args }) {
        order.push(`${element.id}:${ready.has(element)}:${args.value}`);
      } });
    } });
    await Heimdall.invoke("plugin.commands", {}, { target: "#host" });
    return { order, leaked: document.querySelectorAll("heimdall-command").length };
  });
  assert.deepEqual(state.order, ["old", "new-a:true:42", "new-b:true:42"]);
  assert.equal(state.leaked, 0);
}

async function testCommandFailuresAndAbort(page) {
  await installFakeServer(page, { actionResponses: [{ body:
    '<abort></abort><heimdall-command name="missing.command"></heimdall-command>' +
    '<heimdall-command name="test.fail.run" args="bad-json"></heimdall-command>' +
    '<heimdall-command name="test.fail.run" target="#absent"></heimdall-command>' +
    '<heimdall-command name="test.fail.run" args="{&quot;ok&quot;:true}"></heimdall-command>'
  }] });
  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="host">unchanged</div>';
    const errors = [], calls = [];
    document.addEventListener("heimdall:plugin-error", e => errors.push(e.detail.code));
    Heimdall.use({ id: "test.fail", pluginApiVersion: 1, install(api) {
      api.commands.register({ name: "run", handle({ args, element }) { calls.push([args.ok, element]); } });
    } });
    const result = await Heimdall.invoke("plugin.abort", {}, { target: "#host" });
    return { errors, calls, abort: result.abortSwap, text: document.querySelector("#host").textContent };
  });
  assert.deepEqual(state.errors, ["unknown-command", "command-failed", "command-failed"]);
  assert.deepEqual(state.calls, [[true, null]]);
  assert.equal(state.abort, true);
  assert.equal(state.text, "unchanged");
}

async function testSuppressedCommands(page) {
  await installFakeServer(page, { actionResponses: [
    { status: 500, body: '<heimdall-command name="test.suppressed.run"></heimdall-command><p>error</p>' },
    { body: '<heimdall-command name="test.suppressed.run" timing="before"></heimdall-command><redirect url="#done"></redirect>' }
  ] });
  const calls = await page.evaluate(async () => {
    let calls = 0;
    Heimdall.use({ id: "test.suppressed", pluginApiVersion: 1, install(api) {
      api.commands.register({ name: "run", handle() { calls++; } });
    } });
    await Heimdall.invoke("plugin.error", {}, { swap: "none" });
    await Heimdall.invoke("plugin.redirect", {}, { swap: "none" });
    return calls;
  });
  assert.equal(calls, 0);
}

async function testStaleAsyncCommand(page) {
  await installFakeServer(page, { actionResponses: [
    { body: '<heimdall-command name="test.stale.wait" timing="before"></heimdall-command><p id="stale">stale</p><heimdall-command name="test.stale.after"></heimdall-command>' },
    { body: '<p id="winner">winner</p>' }
  ] });
  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="host"></div>';
    let started, signal, after = 0;
    const waiting = new Promise(r => { started = r; });
    Heimdall.use({ id: "test.stale", pluginApiVersion: 1, install(api) {
      api.commands.register({ name: "wait", handle(context) {
        signal = context.signal; started(); return new Promise(() => {});
      } });
      api.commands.register({ name: "after", handle() { after++; } });
    } });
    const options = { target: "#host", sync: "replace", syncGroup: "plugin-stale" };
    const first = Heimdall.invoke("plugin.old", {}, options);
    await waiting;
    await Heimdall.invoke("plugin.new", {}, options);
    const result = await first;
    return { after, cancelled: result.cancelled, aborted: signal.aborted, winner: !!document.querySelector("#winner"), stale: !!document.querySelector("#stale") };
  });
  assert.deepEqual(state, { after: 0, cancelled: true, aborted: true, winner: true, stale: false });
}

async function testCommandTimeout(page) {
  await installFakeServer(page, { actionResponses: [{ body:
    '<heimdall-command name="test.timeout.wait"></heimdall-command><heimdall-command name="test.timeout.next"></heimdall-command>'
  }] });
  const state = await page.evaluate(async () => {
    Heimdall.config.pluginCommandTimeoutMs = 20;
    let next = 0, signal;
    const errors = [];
    document.addEventListener("heimdall:plugin-error", e => errors.push(e.detail.code));
    Heimdall.use({ id: "test.timeout", pluginApiVersion: 1, install(api) {
      api.commands.register({ name: "wait", handle(context) { signal = context.signal; return new Promise(() => {}); } });
      api.commands.register({ name: "next", handle() { next++; } });
    } });
    await Heimdall.invoke("plugin.timeout", {}, { swap: "none" });
    return { next, errors, aborted: signal.aborted };
  });
  assert.deepEqual(state, { next: 1, errors: ["plugin-timeout"], aborted: true });
}

async function testQueuedPayloadTiming(page) {
  await installFakeServer(page, { actionResponses: [{ delayMs: 40, body: "" }, { body: "" }] });
  await page.evaluate(async () => {
    document.body.innerHTML = '<button id="source" heimdall-content-test-queue-commit="plugin.queue" heimdall-content-swap="none" heimdall-sync="queue-latest" heimdall-payload-from="plugin:test.queue.live"></button>';
    let value = 0, send;
    Heimdall.use({ id: "test.queue", pluginApiVersion: 1, install(api) {
      api.payloads.register({ name: "live", timing: "execution", read: () => ({ value }) });
      api.triggers.register({ name: "commit", subscribe({ emit }) { send = emit; } });
    } });
    const started = new Promise(r => document.addEventListener("heimdall:request-before", r, { once: true }));
    const first = send();
    await started;
    value = 1;
    const second = send();
    value = 2;
    await Promise.all([first, second]);
  });
  assert.deepEqual(actionFetches(await getFetches(page)).map(r => r.jsonBody), [{ value: 0 }, { value: 2 }]);
}

async function testSseCommandOrder(page) {
  await installFakeServer(page);
  await page.evaluate(() => {
    document.body.innerHTML = '<div id="stream"></div><div id="host"></div>';
    window.__pluginOrder = [];
    window.EventSource = class {
      constructor() { window.__pluginSource = this; }
      addEventListener() {}
      close() {}
    };
    Heimdall.use({ id: "test.stream", pluginApiVersion: 1, install(api) {
      api.commands.register({ name: "wait", async handle() { await new Promise(r => setTimeout(r, 20)); } });
      api.commands.register({ name: "read", handle({ origin }) {
        window.__pluginOrder.push(`${origin}:${document.querySelector("#host").textContent}`);
      } });
    } });
    Heimdall.sse.connect("plugin-stream", { element: document.querySelector("#stream"), target: "#host", swap: "inner", event: "message" });
  });
  await page.waitForFunction(() => typeof window.__pluginSource?.onmessage === "function");
  await page.evaluate(() => {
    window.__pluginSource.onmessage({ data: '<heimdall-command name="test.stream.wait" timing="before"></heimdall-command>one<heimdall-command name="test.stream.read"></heimdall-command>' });
    window.__pluginSource.onmessage({ data: 'two<heimdall-command name="test.stream.read"></heimdall-command>' });
  });
  await page.waitForFunction(() => window.__pluginOrder.length === 2);
  assert.deepEqual(await page.evaluate(() => window.__pluginOrder), ["sse:one", "sse:two"]);
}

async function testRemovedAsyncMount(page) {
  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="widget" data-widget></div>';
    let complete, signal, disposed = 0;
    Heimdall.use({ id: "test.async-mount", pluginApiVersion: 1, install(api) {
      api.behaviors.register({ name: "widget", selector: "[data-widget]", mount(el, context) {
        signal = context.signal;
        return new Promise(resolve => { complete = () => resolve(() => disposed++); });
      } });
    } });
    document.querySelector("#widget").remove();
    await new Promise(r => setTimeout(r, 0));
    complete();
    await new Promise(r => setTimeout(r, 0));
    return { disposed, aborted: signal.aborted };
  });
  assert.deepEqual(state, { disposed: 1, aborted: true });
}

async function testPayloadFailure(page) {
  await installFakeServer(page);
  const errors = await page.evaluate(async () => {
    document.body.innerHTML = '<button id="button" heimdall-content-click="plugin.invalid" heimdall-payload-from="plugin:test.payload.invalid"></button>';
    const errors = [];
    document.addEventListener("heimdall:plugin-error", e => errors.push(e.detail.code));
    Heimdall.use({ id: "test.payload", pluginApiVersion: 1, install(api) {
      api.payloads.register({ name: "invalid", read() { throw new Error("unavailable"); } });
    } });
    document.querySelector("#button").click();
    await new Promise(r => setTimeout(r, 0));
    return errors;
  });
  assert.deepEqual(errors, ["payload-failed"]);
  assert.equal((await getFetches(page)).length, 0);
}

async function testInitialLoadReadiness(page) {
  await installFakeServer(page);
  await page.evaluate(async () => {
    let ready = false;
    Heimdall.use({ id: "test.initial", pluginApiVersion: 1, install(api) {
      api.behaviors.register({ name: "widget", selector: "[data-widget]", async mount() {
        await new Promise(r => setTimeout(r, 10)); ready = true;
      } });
      api.payloads.register({ name: "value", read() {
        if (!ready) throw new Error("not ready");
        return { ready };
      } });
    } });
    const completed = new Promise(r => document.addEventListener("heimdall:request-finally", r, { once: true }));
    delete document.readyState;
    document.dispatchEvent(new Event("DOMContentLoaded"));
    await completed;
  });
  assert.deepEqual(actionFetches(await getFetches(page))[0].jsonBody, { ready: true });
}

async function testCommandLimits(page) {
  await installFakeServer(page, { actionResponses: [{ body:
    '<heimdall-command name="test.limits.run" args="{&quot;long&quot;:true}"></heimdall-command>' +
    '<heimdall-command name="test.limits.run"></heimdall-command>' +
    '<heimdall-command name="test.limits.run"></heimdall-command>'
  }] });
  const state = await page.evaluate(async () => {
    let calls = 0;
    const errors = [];
    Heimdall.config.pluginCommandMaxArgsChars = 2;
    Heimdall.config.pluginCommandMaxCount = 2;
    document.addEventListener("heimdall:plugin-error", e => errors.push(e.detail.code));
    Heimdall.use({ id: "test.limits", pluginApiVersion: 1, install(api) {
      api.commands.register({ name: "run", handle() { calls++; } });
    } });
    await Heimdall.invoke("plugin.limits", {}, { swap: "none" });
    return { calls, errors };
  });
  assert.deepEqual(state, { calls: 1, errors: ["command-limit", "command-failed"] });
}

async function testSealedRegistration(page) {
  const state = await page.evaluate(() => {
    let saved, removed = 0;
    const errors = [];
    const attempt = callback => { try { callback(); } catch (e) { errors.push(e.message); } };
    Heimdall.use({ id: "test.sealed", pluginApiVersion: 1, install(api) { saved = api; } });
    attempt(() => saved.commands.register({ name: "late", handle() {} }));
    attempt(() => Heimdall.use({ id: "test.attributes", pluginApiVersion: 1, install(api) {
      api.onDispose(() => removed++);
      api.triggers.register({ name: "one", attribute: "heimdall-content-custom", events: ["custom"] });
      api.triggers.register({ name: "two", attribute: "heimdall-content-custom", events: ["custom"] });
    } }));
    attempt(() => Heimdall.use({ id: "test.async-install", pluginApiVersion: 1, async install() {} }));
    return { errors, removed, ids: Heimdall.plugins.list().map(p => p.id) };
  });
  assert.equal(state.errors.length, 3);
  assert.equal(state.removed, 1);
  assert.deepEqual(state.ids, ["test.sealed"]);
}

async function testExamplePlugin(page) {
  await installFakeServer(page, { actionResponses: [{ body:
    '<span>Saved</span><heimdall-command name="company.editor.set-text" target="#editor" args="{&quot;text&quot;:&quot;Normalized&quot;}"></heimdall-command>'
  }] });
  const source = await readFile(new URL("../../examples/editor-plugin.js", import.meta.url), "utf8");
  await page.addScriptTag({ type: "module", content: source.replace("export default editorPlugin;", "window.__editorPlugin = editorPlugin;") });
  await page.waitForFunction(() => !!window.__editorPlugin);
  const value = await page.evaluate(async () => {
    document.body.innerHTML = '<textarea id="editor" data-company-editor heimdall-content-company-editor-commit="documents.save" heimdall-payload-from="plugin:company.editor.document" heimdall-content-target="#status"></textarea><div id="status"></div>';
    Heimdall.use(window.__editorPlugin, { commitKey: "Enter" });
    const done = new Promise(r => document.addEventListener("heimdall:request-finally", r, { once: true }));
    const editor = document.querySelector("#editor");
    editor.value = "User input";
    editor.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", ctrlKey: true, bubbles: true, cancelable: true }));
    await done;
    return { text: editor.value, status: document.querySelector("#status").textContent };
  });
  assert.deepEqual(value, { text: "Normalized", status: "Saved" });
  assert.deepEqual(actionFetches(await getFetches(page))[0].jsonBody, { text: "User input" });
}

async function testMutationLifecycle(page) {
  await installFakeServer(page, { actionResponses: [
    { body: '<mutation heimdall-content-target="#widget"><mutation-attr name="data-widget" value="true"></mutation-attr></mutation>' },
    { body: '<mutation heimdall-content-target="#widget"><mutation-attr name="data-value" value="two"></mutation-attr></mutation>' },
    { body: '<mutation heimdall-content-target="#widget"><mutation-attr name="data-widget" remove="true"></mutation-attr></mutation>' }
  ] });
  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="widget" data-value="one"></div>';
    Heimdall.config.observeDom = false;
    Heimdall._observer.disconnect();
    let mounted = 0, updated = 0, disposed = 0;
    Heimdall.use({ id: "test.mutations", pluginApiVersion: 1, install(api) {
      api.behaviors.register({ name: "widget", selector: "[data-widget]", observedAttributes: ["data-value"], mount() {
        mounted++; return { update() { updated++; }, dispose() { disposed++; } };
      } });
    } });
    for (let i = 0; i < 3; i++) await Heimdall.invoke("plugin.mutate", {}, { swap: "none" });
    return { mounted, updated, disposed };
  });
  assert.deepEqual(state, { mounted: 1, updated: 1, disposed: 1 });
}

async function testPluginInvocationRemoval(page) {
  await installFakeServer(page, { actionResponses: [{ delayMs: 60, body: '<p id="late">late</p>' }] });
  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="host"></div>';
    let invoke;
    Heimdall.use({ id: "test.invoking", pluginApiVersion: 1, install(api) { invoke = api.invoke; } });
    const started = new Promise(r => document.addEventListener("heimdall:request-before", r, { once: true }));
    const request = invoke("plugin.running", {}, { target: "#host" });
    await started;
    Heimdall.plugins.remove("test.invoking");
    const result = await request;
    return { cancelled: result.cancelled, late: !!document.querySelector("#late") };
  });
  assert.deepEqual(state, { cancelled: true, late: false });
}

async function testTriggerSnapshots(page) {
  await installFakeServer(page, { actionResponses: [{ delayMs: 40, body: "" }, { body: "" }] });
  await page.evaluate(async () => {
    document.body.innerHTML = '<button heimdall-content-test-snapshot-commit="plugin.snapshot" heimdall-content-swap="none" heimdall-sync="queue-latest"></button>';
    let send;
    Heimdall.use({ id: "test.snapshot", pluginApiVersion: 1, install(api) {
      api.triggers.register({ name: "commit", subscribe({ emit }) { send = emit; } });
    } });
    const started = new Promise(r => document.addEventListener("heimdall:request-before", r, { once: true }));
    const first = send(null, { payload: { value: 0 } });
    await started;
    const value = { value: 1 };
    const second = send(null, { payload: value });
    value.value = 2;
    await Promise.all([first, second]);
  });
  assert.deepEqual(actionFetches(await getFetches(page)).map(r => r.jsonBody), [{ value: 0 }, { value: 1 }]);
}

async function testSseQueueBound(page) {
  await installFakeServer(page);
  await page.evaluate(() => {
    document.body.innerHTML = '<div id="stream"></div><div id="host"></div>';
    window.__queueErrors = [];
    document.addEventListener("heimdall:plugin-error", e => window.__queueErrors.push(e.detail.code));
    Heimdall.config.pluginSseQueueLimit = 1;
    window.EventSource = class {
      constructor() { window.__queueSource = this; }
      addEventListener() {}
      close() { this.closed = true; }
    };
    Heimdall.use({ id: "test.queue-limit", pluginApiVersion: 1, install(api) {
      api.commands.register({ name: "wait", handle: () => new Promise(() => {}) });
    } });
    Heimdall.sse.connect("plugin-queue-limit", { element: document.querySelector("#stream"), target: "#host", swap: "inner", event: "message" });
  });
  await page.waitForFunction(() => typeof window.__queueSource?.onmessage === "function");
  await page.evaluate(() => {
    const data = '<heimdall-command name="test.queue-limit.wait" timing="before"></heimdall-command><p id="late-stream">late</p>';
    window.__queueSource.onmessage({ data });
    window.__queueSource.onmessage({ data });
  });
  assert.deepEqual(await page.evaluate(() => ({
    errors: window.__queueErrors, closed: window.__queueSource.closed, late: !!document.querySelector("#late-stream")
  })), { errors: ["sse-command-queue-full"], closed: true, late: false });
}

async function testSelfRemoval(page) {
  await installFakeServer(page, { actionResponses: [{ body:
    '<heimdall-command name="test.self-removal.remove"></heimdall-command>'
  }] });
  const state = await page.evaluate(async () => {
    const unhandled = [];
    window.addEventListener("unhandledrejection", event => unhandled.push(event.reason.message));
    Heimdall.use({ id: "test.self-removal", pluginApiVersion: 1, install(api) {
      api.commands.register({ name: "remove", handle() {
        Heimdall.plugins.remove("test.self-removal");
        return Promise.reject(new Error("cancelled plugin continuation"));
      } });
    } });
    await Heimdall.invoke("plugin.self-removal", {}, { swap: "none" });
    await new Promise(r => setTimeout(r, 0));
    return { unhandled, installed: Heimdall.plugins.list().length };
  });
  assert.deepEqual(state, { unhandled: [], installed: 0 });
}

async function testPrimitiveFailures(page) {
  await installFakeServer(page, { actionResponses: [{ body:
    '<heimdall-command name="test.primitives.fail"></heimdall-command><heimdall-command name="test.primitives.next"></heimdall-command>'
  }] });
  const state = await page.evaluate(async () => {
    let next = false, reserved = false;
    const errors = [];
    document.addEventListener("heimdall:plugin-error", e => errors.push(e.detail.code));
    Heimdall.use({ id: "test.primitives", pluginApiVersion: 1, install(api) {
      api.commands.register({ name: "fail", handle() { throw null; } });
      api.commands.register({ name: "next", handle() { next = true; } });
    } });
    try {
      Heimdall.use({ id: "test.core-option", pluginApiVersion: 1, install(api) {
        api.triggers.register({ name: "bad", attribute: "heimdall-content-target", events: ["commit"] });
      } });
    } catch { reserved = true; }
    await Heimdall.invoke("plugin.failures", {}, { swap: "none" });
    return { next, reserved, errors };
  });
  assert.deepEqual(state, { next: true, reserved: true, errors: ["install-failed", "command-failed"] });
}

async function testReadinessAfterRemount(page) {
  await installFakeServer(page, { actionResponses: [{ body:
    '<div id="widget" data-widget data-options="one"></div><heimdall-command name="test.remount.read" target="#widget"></heimdall-command>'
  }] });
  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="host"></div>';
    const mounts = [], readings = [], live = new WeakMap();
    Heimdall.use({ id: "test.remount", pluginApiVersion: 1, install(api) {
      api.behaviors.register({ name: "widget", selector: "[data-widget]", observedAttributes: ["data-options"], async mount(el) {
        const options = el.getAttribute("data-options");
        mounts.push(options);
        if (options === "one") el.setAttribute("data-options", "two");
        await new Promise(r => setTimeout(r, 10));
        live.set(el, options);
        return () => live.delete(el);
      } });
      api.commands.register({ name: "read", handle({ element }) { readings.push(live.get(element)); } });
    } });
    await Heimdall.invoke("plugin.remount", {}, { target: "#host" });
    return { mounts, readings };
  });
  assert.deepEqual(state, { mounts: ["one", "two"], readings: ["two"] });
}

export const tests = [
  ["validates plugin installation, dependencies, collisions, and rollback", testRegistration],
  ["manages plugin element setup, moves, updates, and removal", testLifetimes],
  ["mounts every plugin swap root and preserves cancelled swaps", testSwapLifetimes],
  ["routes plugin triggers through debounce, scope, payloads, and security", testCustomTrigger],
  ["supports plugin subscriptions and releases their resources", testSubscriptionAndRemoval],
  ["uses plugin payload readers with built-in form submission", testBuiltinPayload],
  ["orders plugin commands around swaps and asynchronous target readiness", testCommandReadiness],
  ["isolates plugin command failures and retains commands on abort", testCommandFailuresAndAbort],
  ["suppresses plugin commands on redirects and error responses", testSuppressedCommands],
  ["cancels stale asynchronous plugin commands before later effects", testStaleAsyncCommand],
  ["bounds asynchronous plugin command execution", testCommandTimeout],
  ["refreshes execution-time plugin payloads when queued work starts", testQueuedPayloadTiming],
  ["serializes asynchronous plugin commands across Bifrost messages", testSseCommandOrder],
  ["disposes plugin initialization that completes after element removal", testRemovedAsyncMount],
  ["fails closed when a plugin payload reader throws", testPayloadFailure],
  ["waits for plugin behavior readiness before initial load payloads", testInitialLoadReadiness, { runtimePage: {
    initialBody: '<div data-widget heimdall-content-load="plugin.initial" heimdall-payload-from="plugin:test.initial.value" heimdall-content-swap="none"></div>',
    beforeRuntime: async page => page.evaluate(() => Object.defineProperty(document, "readyState", { value: "loading", configurable: true }))
  } }],
  ["bounds plugin command count and JSON argument size", testCommandLimits],
  ["seals plugin registration and rejects conflicting or async installations", testSealedRegistration],
  ["runs the shipped editor plugin example end to end", testExamplePlugin],
  ["reconciles plugin behaviors changed by mutation directives without DOM observation", testMutationLifecycle],
  ["cancels outstanding plugin-owned invocations on removal", testPluginInvocationRemoval],
  ["snapshots explicit plugin trigger payloads while queued", testTriggerSnapshots],
  ["bounds pending plugin Bifrost command messages", testSseQueueBound],
  ["handles plugin self-removal without unhandled asynchronous failures", testSelfRemoval],
  ["isolates primitive plugin exceptions and protects core option attributes", testPrimitiveFailures],
  ["follows async plugin remounts before declaring a command target ready", testReadinessAfterRemount]
];
