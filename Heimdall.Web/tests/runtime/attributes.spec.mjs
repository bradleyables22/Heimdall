import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { installFakeServer, actionFetches, getFetches } from "../helpers/runtime-page.mjs";

async function shortcuts(page, minified = false) {
  await page.addScriptTag({ content: await readFile(new URL(
    `../../wwwroot/heimdall-shortcuts${minified ? ".min" : ""}.js`, import.meta.url), "utf8") });
}

async function requests(page, count) {
  await page.waitForFunction(expected => window.__heimdallFetches.filter(f =>
    f.url.includes("/content/invocations")).length >= expected, count);
  return actionFetches(await getFetches(page));
}

async function testCatalog(page) {
  const sources = await Promise.all([
    readFile(new URL("../../../Heimdall.Rendering/Rendering/Heimdall/Core/Attrs.cs", import.meta.url), "utf8"),
    readFile(new URL("../../../Heimdall.Rendering/Rendering/Html/LocalTimeFormatting.cs", import.meta.url), "utf8")
  ]);
  const expected = [...sources[0].matchAll(/public const string \w+ = "([^"]+)"/g)].map(match =>
    match[1] === "data-heimdall-state-" ? `${match[1]}*` : match[1]);
  expected.push(...[...sources[1].matchAll(/const string \w+Attribute = "([^"]+)"/g)].map(match => match[1]));
  const catalog = await page.evaluate(() => ({
    entries: Heimdall.attributes.list(),
    frozen: Object.isFrozen(Heimdall.attributes.list()) && Heimdall.attributes.list().every(entry =>
      Object.isFrozen(entry) && Object.isFrozen(entry.aliases))
  }));
  assert.equal(catalog.entries.length, 39);
  assert.deepEqual(catalog.entries.map(entry => entry.name).sort(), expected.sort());
  assert.ok(catalog.entries.every(entry => entry.owner === "heimdall" && entry.aliases.length === 0));
  assert.ok(catalog.frozen);
}

async function testOptIn(page) {
  await installFakeServer(page);
  await page.evaluate(() => {
    document.body.innerHTML = '<button id="save" hd-click="alias.save" hd-swap="none">Save</button>';
    document.querySelector("#save").click();
  });
  assert.equal(actionFetches(await getFetches(page)).length, 0);
  await shortcuts(page, true);
  const aliases = await page.evaluate(() => {
    document.querySelector("#save").click();
    return Heimdall.attributes.list().map(entry => ({ name: entry.name, aliases: entry.aliases }));
  });
  assert.ok(aliases.every(entry => entry.aliases.length === 1));
  assert.deepEqual(aliases.find(entry => entry.name === "heimdall-payload-from").aliases, ["hd-from"]);
  assert.deepEqual(aliases.find(entry => entry.name === "data-heimdall-state-*").aliases, ["data-hd-state-*"]);
  assert.equal((await requests(page, 1))[0].headers["x-heimdall-content-invocation"], "alias.save");
}

async function testOriginalPrecedence(page) {
  await installFakeServer(page, { actionResponses: [{ body: '<strong id="saved">Saved</strong>' }] });
  await shortcuts(page);
  await page.evaluate(() => {
    document.body.innerHTML = `<div heimdall-content-click="outer">
      <button id="save" heimdall-content-click="original.save" hd-click="ignored.save"
        hd-target="#target" hd-swap="inner" hd-payload='{"id":42}'>Save</button>
      </div><div id="target">Old</div>`;
    document.querySelector("#save").click();
  });
  await page.waitForSelector("#saved");
  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 1);
  assert.equal(actions[0].headers["x-heimdall-content-invocation"], "original.save");
  assert.deepEqual(actions[0].jsonBody, { id: 42 });
  assert.equal(await page.locator("#save").getAttribute("heimdall-content-target"), null);
}

async function testFormsAndPayloadRefs(page) {
  await installFakeServer(page);
  await shortcuts(page);
  const state = await page.evaluate(async () => {
    document.body.innerHTML = `<form id="form" hd-submit="alias.form" hd-swap="none">
      <input name="Name" value="Ada" />
      <button id="input" type="button" hd-click="alias.closest" hd-from="closest-form" hd-swap="none">Send</button>
      </form><button id="ref" hd-click="alias.ref" hd-ref="testAliasPayload" hd-swap="none">Ref</button>`;
    window.testAliasPayload = { source: "reference" };
    const send = element => new Promise(resolve => {
      document.addEventListener("heimdall:request-finally", resolve, { once: true });
      if (element.tagName === "FORM") element.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true }));
      else element.click();
    });
    await send(document.querySelector("#form"));
    await send(document.querySelector("#input"));
    await send(document.querySelector("#ref"));
    return document.querySelector("#form").hasAttribute("heimdall-content-submit");
  });
  assert.equal(state, false);
  assert.deepEqual(actionFetches(await getFetches(page)).map(action => action.jsonBody), [
    { Name: "Ada" }, { Name: "Ada" }, { source: "reference" }
  ]);
}

async function testDelegatedTriggers(page) {
  await installFakeServer(page);
  await shortcuts(page);
  const state = await page.evaluate(async () => {
    document.body.innerHTML = `<input id="input" hd-input="alias.input" hd-debounce="10" hd-swap="none" />
      <input id="change" hd-change="alias.change" hd-debounce="10" hd-swap="none" />
      <input id="key" hd-keydown="alias.key" hd-key="Enter" hd-prevent-default="true" hd-swap="none" />
      <input id="blur" hd-blur="alias.blur" hd-swap="none" />
      <div id="hover" hd-hover="alias.hover" hd-hover-delay="10" hd-swap="none"></div>
      <button id="scope" hd-click="alias.scope" hd-scope="self" hd-swap="none"><span id="child">Child</span></button>
      <div hd-click="alias.blocked"><span id="ignore" hd-ignore="click">Ignored</span></div>`;
    const send = (id, event) => new Promise(resolve => {
      document.addEventListener("heimdall:request-finally", resolve, { once: true });
      document.getElementById(id).dispatchEvent(event);
    });
    document.querySelector("#child").click();
    document.querySelector("#ignore").click();
    document.querySelector("#key").dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true }));
    await send("input", new Event("input", { bubbles: true }));
    await send("change", new Event("change", { bubbles: true }));
    const enter = new KeyboardEvent("keydown", { key: "Enter", bubbles: true, cancelable: true });
    await send("key", enter);
    await send("blur", new FocusEvent("focusout", { bubbles: true }));
    await send("hover", new MouseEvent("mouseover", { bubbles: true }));
    await send("scope", new MouseEvent("click", { bubbles: true, button: 0 }));
    return enter.defaultPrevented;
  });
  assert.ok(state);
  assert.deepEqual(actionFetches(await getFetches(page)).map(action => action.headers["x-heimdall-content-invocation"]),
    ["alias.input", "alias.change", "alias.key", "alias.blur", "alias.hover", "alias.scope"]);
}

async function testNamedStateQueue(page) {
  await installFakeServer(page, { actionResponses: [
    { delayMs: 35, body: `<mutation heimdall-content-target="#state"><mutation-attr name="data-hd-state-cart" value='{"count":1}'></mutation-attr></mutation>` },
    { body: '<span>Done</span>' }
  ] });
  await shortcuts(page);
  await page.evaluate(async () => {
    document.body.innerHTML = `<div id="state" data-hd-state-cart='{"count":0}'>
      <button id="send" hd-click="alias.state" hd-from="closest-state:cart" hd-swap="none"
        hd-disable="false" hd-sync="queue-latest" hd-sync-group="cart">Update</button></div>`;
    let start, finish, count = 0;
    const started = new Promise(resolve => { start = resolve; });
    const done = new Promise(resolve => { finish = resolve; });
    document.addEventListener("heimdall:request-before", start, { once: true });
    document.addEventListener("heimdall:request-finally", () => { if (++count === 2) finish(); });
    document.querySelector("#send").click();
    await started;
    document.querySelector("#send").click();
    await done;
  });
  assert.deepEqual(actionFetches(await getFetches(page)).map(action => action.jsonBody), [{ count: 0 }, { count: 1 }]);
}

async function testUnnamedStateAndSync(page) {
  await installFakeServer(page, { actionResponses: [{ delayMs: 40 }] });
  await shortcuts(page);
  await page.evaluate(async () => {
    document.body.innerHTML = `<div data-hd-state='{"id":7}'>
      <button id="a" hd-click="alias.first" hd-from="closest-state" hd-sync="drop" hd-sync-group="shared" hd-swap="none">A</button>
      <button id="b" hd-click="alias.dropped" hd-sync="drop" hd-sync-group="shared" hd-swap="none">B</button></div>`;
    const started = new Promise(resolve => document.addEventListener("heimdall:request-before", resolve, { once: true }));
    const done = new Promise(resolve => document.addEventListener("heimdall:request-finally", resolve, { once: true }));
    document.querySelector("#a").click();
    await started;
    document.querySelector("#b").click();
    await done;
  });
  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 1);
  assert.deepEqual(actions[0].jsonBody, { id: 7 });
}

async function testLoadPollAndRemoval(page) {
  await installFakeServer(page);
  await page.evaluate(() => {
    document.body.innerHTML = '<div id="poll" hd-load="alias.poll" hd-poll="25" hd-swap="none"></div>';
  });
  await shortcuts(page);
  await requests(page, 2);
  await page.evaluate(() => {
    // Another registry edit must preserve the existing load and timer identities.
    Heimdall.attributes.alias({ "heimdall-key": "custom-key" });
    Heimdall.plugins.remove("hd.shortcuts");
  });
  const count = actionFetches(await getFetches(page)).length;
  await page.waitForTimeout(85);
  assert.equal(actionFetches(await getFetches(page)).length, count);
  assert.equal(await page.evaluate(() => document.querySelector("#poll").__heimdallPollBound), false);
}

async function testVisibleScrollAndLifecycle(page) {
  await installFakeServer(page);
  await page.evaluate(() => {
    window.IntersectionObserver = class {
      constructor(callback) { this.callback = callback; this.elements = new Set(); window.testObserver = this; }
      observe(element) { this.elements.add(element); }
      unobserve(element) { this.elements.delete(element); }
    };
  });
  await shortcuts(page);
  await page.evaluate(async () => {
    document.body.innerHTML = `<div id="visible" hd-visible="alias.visible" hd-visible-once="false" hd-swap="none"></div>
      <div id="scroll" hd-scroll="alias.scroll" hd-scroll-threshold="60" hd-swap="none"></div>
      <div hd-document-visible="alias.document" hd-swap="none"></div>
      <div hd-online="alias.online" hd-swap="none"></div>`;
    Heimdall.boot(document);
    const wait = run => new Promise(resolve => {
      document.addEventListener("heimdall:request-finally", resolve, { once: true }); run();
    });
    const visible = document.querySelector("#visible");
    const fire = () => testObserver.callback([{ target: visible, isIntersecting: true }]);
    await wait(fire);
    await wait(fire);
    const scroll = document.querySelector("#scroll");
    Object.defineProperties(scroll, { scrollTop: { value: 0 }, clientHeight: { value: 100 }, scrollHeight: { value: 150 } });
    await wait(() => scroll.dispatchEvent(new Event("scroll")));
    Object.defineProperty(document, "visibilityState", { configurable: true, value: "visible" });
    await wait(() => document.dispatchEvent(new Event("visibilitychange")));
    await wait(() => window.dispatchEvent(new Event("online")));
    Heimdall.plugins.remove("hd.shortcuts");
    if (testObserver.elements.has(visible)) throw new Error("Removed alias left a visibility subscription active.");
  });
  assert.deepEqual(actionFetches(await getFetches(page)).map(action => action.headers["x-heimdall-content-invocation"]),
    ["alias.visible", "alias.visible", "alias.scroll", "alias.document", "alias.online"]);
}

async function testTimeObservationAndMutations(page) {
  await installFakeServer(page, { actionResponses: [{ body: `<mutation heimdall-content-target="#time">
    <mutation-attr name="hd-time-format" value="yyyy"></mutation-attr></mutation>` }] });
  await shortcuts(page);
  await page.evaluate(() => {
    document.body.innerHTML = '<span id="time" hd-time="2026-08-26T18:30:05Z" hd-time-format="yyyy-MM-dd">Fallback</span>';
  });
  await page.waitForFunction(() => document.querySelector("#time").textContent === "2026-08-26");
  await page.evaluate(() => document.querySelector("#time").setAttribute("hd-time-format", "yyyy-MM"));
  await page.waitForFunction(() => document.querySelector("#time").textContent === "2026-08");
  await page.evaluate(async () => {
    Heimdall.config.observeDom = false;
    Heimdall._observer.disconnect();
    await Heimdall.invoke("alias.mutate", {}, { swap: "none" });
  });
  assert.equal(await page.locator("#time").textContent(), "2026");
}

async function testSse(page) {
  await installFakeServer(page);
  await page.evaluate(() => {
    window.sources = [];
    window.EventSource = class {
      constructor(url) { this.url = url; this.listeners = {}; this.closed = false; sources.push(this); }
      addEventListener(name, listener) { this.listeners[name] = listener; }
      close() { this.closed = true; }
    };
  });
  await shortcuts(page);
  await page.evaluate(() => {
    document.body.innerHTML = `<div id="source" hd-sse="topic:alias" hd-sse-target="#target" hd-sse-swap="inner" hd-sse-event="update"></div>
      <div id="disabled" hd-sse-topic="topic:disabled" hd-sse-disable="true"></div><div id="target"></div>`;
  });
  await page.waitForFunction(() => sources.length === 1);
  await page.evaluate(() => sources[0].listeners.update({ data: "<b>Streamed</b>" }));
  await page.waitForFunction(() => document.querySelector("#target").textContent === "Streamed");
  await page.evaluate(() => document.querySelector("#source").setAttribute("hd-sse-disable", "true"));
  await page.waitForFunction(() => sources[0].closed);
  await page.evaluate(() => {
    document.querySelector("#source").remove();
    document.querySelector("#disabled").setAttribute("hd-sse-disable", "false");
  });
  await page.waitForFunction(() => sources.length === 2);
  assert.equal(new URL(await page.evaluate(() => sources[1].url)).searchParams.get("topic"), "topic:disabled");
  await page.evaluate(() => Heimdall.plugins.remove("hd.shortcuts"));
  assert.equal(await page.evaluate(() => sources[1].closed), true);
}

async function testCollisionsAndUndo(page) {
  const state = await page.evaluate(() => {
    const api = Heimdall.attributes, errors = [];
    const attempt = run => { try { run(); } catch (error) { errors.push(error.message); } };
    const removeFirst = api.alias({ "heimdall-content-click": "team-action" });
    attempt(() => api.alias({ "heimdall-content-submit": "team-action" }));
    const removeOverride = api.alias({ "heimdall-content-submit": "team-action" }, { replace: true });
    const overridden = api.names("heimdall-content-submit");
    removeOverride(); removeOverride();
    const restored = api.names("heimdall-content-click");
    attempt(() => api.alias({ "heimdall-content-submit": "heimdall-content-click" }, { replace: true }));
    attempt(() => api.alias({ "heimdall-content-click": "id" }));
    attempt(() => api.alias({ "heimdall-content-click": "onclick" }));
    attempt(() => api.alias({ "heimdall-content-click": "bad name" }));
    attempt(() => api.alias({ "url": "hd-url" }));
    attempt(() => api.alias({ "data-heimdall-state-*": "data-heimdall-*" }));
    attempt(() => api.alias({ "heimdall-key": "rollback-key", missing: "unknown" }));
    const atomic = !api.names("heimdall-key").includes("rollback-key");
    const removePrefix = api.alias({ "data-heimdall-state-*": "team-state-*" });
    attempt(() => api.alias({ "heimdall-content-click": "team-state-cart" }));
    removePrefix(); removeFirst();
    return { errors, overridden, restored, atomic, cleaned: api.names("heimdall-content-click") };
  });
  assert.equal(state.errors.length, 9);
  assert.deepEqual(state.overridden, ["heimdall-content-submit", "team-action"]);
  assert.deepEqual(state.restored, ["heimdall-content-click", "team-action"]);
  assert.deepEqual(state.cleaned, ["heimdall-content-click"]);
  assert.ok(state.atomic);
}

async function testPluginSynergy(page) {
  await installFakeServer(page);
  await shortcuts(page);
  const state = await page.evaluate(async () => {
    const registry = Heimdall.attributes;
    document.body.innerHTML = `<div company-commit="outer">
      <button id="commit" hd-commit="alias.plugin" company-commit="alias.plugin"
        hd-payload='{"id":5}' hd-swap="none" hd-debounce="5"></button></div>`;
    Heimdall.use({ id: "company.editor", pluginApiVersion: 1, install(api) {
      api.triggers.register({ name: "commit", attribute: "company-commit", aliases: ["hd-commit"], events: ["commit"] });
      api.attributes.alias({ "heimdall-key": "editor-key" });
    } });
    const entry = registry.list().find(entry => entry.name === "company-commit");
    const done = new Promise(resolve => document.addEventListener("heimdall:request-finally", resolve, { once: true }));
    document.querySelector("#commit").dispatchEvent(new Event("commit", { bubbles: true }));
    await done;
    const remove = registry.alias({ "company-commit": "app-commit" });
    document.querySelector("#commit").removeAttribute("company-commit");
    document.querySelector("#commit").removeAttribute("hd-commit");
    document.querySelector("#commit").setAttribute("app-commit", "alias.app-plugin");
    const second = new Promise(resolve => document.addEventListener("heimdall:request-finally", resolve, { once: true }));
    document.querySelector("#commit").dispatchEvent(new Event("commit", { bubbles: true }));
    await second;
    Heimdall.plugins.remove("company.editor");
    remove();
    return { entry, cleaned: !registry.list().some(entry => entry.owner === "company.editor"),
      key: registry.names("heimdall-key"), names: registry.names("company-commit") };
  });
  assert.equal(state.entry.owner, "company.editor");
  assert.deepEqual(state.entry.aliases, ["hd-commit"]);
  assert.ok(state.cleaned);
  assert.deepEqual(state.key, ["heimdall-key", "hd-key"]);
  assert.deepEqual(state.names, ["company-commit"]);
  const actions = actionFetches(await getFetches(page));
  assert.deepEqual(actions.map(action => action.headers["x-heimdall-content-invocation"]), ["alias.plugin", "alias.app-plugin"]);
  assert.deepEqual(actions[0].jsonBody, { id: 5 });
}

async function testPluginRollback(page) {
  const state = await page.evaluate(() => {
    let failed = false;
    try {
      Heimdall.use({ id: "broken.editor", pluginApiVersion: 1, install(api) {
        api.triggers.register({ name: "commit", aliases: ["broken-commit"], events: ["commit"] });
        api.attributes.alias({ "heimdall-content-click": "broken-click" });
        throw new Error("Rollback");
      } });
    } catch { failed = true; }
    Heimdall.use({ id: "replacement.editor", pluginApiVersion: 1, install(api) {
      api.triggers.register({ name: "commit", aliases: ["broken-commit"], events: ["commit"] });
    } });
    return { failed, click: Heimdall.attributes.names("heimdall-content-click"),
      entries: Heimdall.attributes.list().filter(entry => entry.category === "plugin-trigger") };
  });
  assert.ok(state.failed);
  assert.deepEqual(state.click, ["heimdall-content-click"]);
  assert.equal(state.entries.length, 1);
  assert.equal(state.entries[0].owner, "replacement.editor");
}

async function testCanonicalResponses(page) {
  await installFakeServer(page, { actionResponses: [{ body: `
    <invocation hd-target="#ignored"><b>Wrong</b></invocation>
    <invocation heimdall-content-target="#target"><b>Canonical</b></invocation>
    <mutation heimdall-content-target="#ignored"><mutation-attr name="title" value="kept"></mutation-attr></mutation>` }] });
  await shortcuts(page);
  await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target"></div><div id="ignored">Original</div>';
    await Heimdall.invoke("alias.response", {}, { swap: "none" });
  });
  assert.equal(await page.locator("#target").textContent(), "Canonical");
  assert.equal(await page.locator("#ignored").textContent(), "Original");
  assert.equal(await page.locator("#ignored").getAttribute("title"), "kept");
}

async function testCaughtRegistrationAndSealing(page) {
  await shortcuts(page);
  const state = await page.evaluate(() => {
    document.body.innerHTML = '<div data-editor></div>';
    const errors = [];
    Heimdall.use({ id: "caught.editor", pluginApiVersion: 1, install(api) {
      try { api.triggers.register({ name: "commit", aliases: ["hd-click"], events: ["commit"] }); }
      catch (error) { errors.push(error.message); }
      api.triggers.register({ name: "commit", aliases: ["hd-commit"], events: ["commit"] });
      api.behaviors.register({ name: "editor", selector: "[data-editor]", mount() {
        try { api.attributes.alias({ "heimdall-key": "leaked-key" }); }
        catch (error) { errors.push(error.message); }
        try { api.triggers.register({ name: "late", events: ["late"] }); }
        catch (error) { errors.push(error.message); }
      } });
    } });
    return { errors, entries: Heimdall.attributes.list().filter(entry => entry.owner === "caught.editor"),
      key: Heimdall.attributes.names("heimdall-key") };
  });
  assert.equal(state.errors.length, 3);
  assert.equal(state.entries.length, 1);
  assert.deepEqual(state.entries[0].aliases, ["hd-commit"]);
  assert.deepEqual(state.key, ["heimdall-key", "hd-key"]);
}

async function testAliasProviders(page) {
  const state = await page.evaluate(() => {
    const api = Heimdall.attributes;
    const undoApp = api.alias({ "heimdall-content-click": "shared-click" });
    Heimdall.use({ id: "team.shortcuts", pluginApiVersion: 1, install(plugin) {
      plugin.attributes.alias({ "heimdall-content-click": "shared-click" });
      plugin.attributes.alias({ "heimdall-key": "shared-key" });
    } });
    const undoOverride = api.alias({ "heimdall-content-submit": "shared-click" }, { replace: true });
    Heimdall.plugins.remove("team.shortcuts");
    undoOverride();
    const surviving = api.names("heimdall-content-click");
    const key = api.names("heimdall-key");
    undoApp();
    return { surviving, key, cleaned: api.names("heimdall-content-click") };
  });
  assert.deepEqual(state.surviving, ["heimdall-content-click", "shared-click"]);
  assert.deepEqual(state.key, ["heimdall-key"]);
  assert.deepEqual(state.cleaned, ["heimdall-content-click"]);
}

async function testPendingDelegates(page) {
  await installFakeServer(page);
  await shortcuts(page);
  await page.evaluate(() => {
    document.body.innerHTML = `<input id="input" hd-input="removed.input" hd-debounce="30" />
      <input id="change" hd-change="removed.change" hd-debounce="30" />
      <div id="hover" hd-hover="removed.hover" hd-hover-delay="30"></div>`;
    document.querySelector("#input").dispatchEvent(new Event("input", { bubbles: true }));
    document.querySelector("#change").dispatchEvent(new Event("change", { bubbles: true }));
    document.querySelector("#hover").dispatchEvent(new MouseEvent("mouseover", { bubbles: true }));
    Heimdall.plugins.remove("hd.shortcuts");
  });
  await page.waitForTimeout(65);
  assert.equal(actionFetches(await getFetches(page)).length, 0);
}

async function testBehaviorReaders(page) {
  await shortcuts(page);
  await page.evaluate(() => {
    document.body.innerHTML = '<div id="editor" data-editor hd-debounce="10"></div>';
    window.observedValues = [];
    Heimdall.use({ id: "reader.editor", pluginApiVersion: 1, install(api) {
      api.behaviors.register({ name: "editor", selector: "[data-editor]", observedAttributes: ["heimdall-debounce"],
        mount(element) {
          observedValues.push(api.attributes.get(element, "heimdall-debounce"));
          return { update() { observedValues.push(api.attributes.get(element, "heimdall-debounce")); } };
        }
      });
    } });
    document.querySelector("#editor").setAttribute("hd-debounce", "20");
  });
  await page.waitForFunction(() => observedValues.length === 2);
  await page.evaluate(() => Heimdall.plugins.remove("hd.shortcuts"));
  assert.deepEqual(await page.evaluate(() => observedValues), ["10", "20", null]);
}

async function testBeforeStartup(page) {
  assert.equal(actionFetches(await getFetches(page)).length, 0);
  await shortcuts(page);
  assert.equal(actionFetches(await getFetches(page)).length, 0);
  await page.evaluate(() => document.dispatchEvent(new Event("DOMContentLoaded")));
  const actions = await requests(page, 1);
  assert.equal(actions[0].headers["x-heimdall-content-invocation"], "alias.initial");
  assert.deepEqual(actions[0].jsonBody, { initial: true });
}

export const tests = [
  ["attribute registry exposes every built-in name and the named-state family", testCatalog],
  ["attribute shortcuts are opt-in and the minified plugin installs the complete preset", testOptIn],
  ["attribute shortcuts preserve originals, nearest routing, and original-name precedence", testOriginalPrecedence],
  ["attribute shortcuts serialize submit, closest-form, and reference payloads", testFormsAndPayloadRefs],
  ["attribute shortcuts support delegated triggers, modifiers, scope, and ignore", testDelegatedTriggers],
  ["attribute shortcuts refresh named state for queued requests", testNamedStateQueue],
  ["attribute shortcuts read unnamed state and coordinate synchronization groups", testUnnamedStateAndSync],
  ["attribute shortcuts reconcile late load, polling, and preset removal", testLoadPollAndRemoval],
  ["attribute shortcuts cover visibility, scroll, and page lifecycle triggers", testVisibleScrollAndLifecycle],
  ["attribute shortcuts localize time and reconcile mutation attributes without observation", testTimeObservationAndMutations, { runtimePage: { timezoneId: "UTC" } }],
  ["attribute shortcuts support SSE aliases, options, observation, and teardown", testSse],
  ["attribute aliases validate collisions atomically and support explicit override and undo", testCollisionsAndUndo],
  ["attribute aliases share core discovery, modifiers, and cleanup with plugins", testPluginSynergy],
  ["attribute aliases roll back failed plugin installations", testPluginRollback],
  ["attribute shortcuts leave response directive parsing canonical", testCanonicalResponses],
  ["attribute aliases keep caught registration failures atomic and seal APIs before mounting", testCaughtRegistrationAndSealing],
  ["attribute aliases retain independent providers and restore live mappings", testAliasProviders],
  ["attribute shortcuts stop pending delegated events after alias removal", testPendingDelegates],
  ["attribute aliases resolve plugin behavior readers and observed attribute signatures", testBehaviorReaders],
  ["attribute shortcuts register before startup and boot initial markup once", testBeforeStartup, { runtimePage: {
    initialBody: '<div hd-load="alias.initial" hd-payload=\'{"initial":true}\' hd-swap="none"></div>',
    beforeRuntime: async page => {
      await installFakeServer(page);
      await page.evaluate(() => Object.defineProperty(document, "readyState", { configurable: true, value: "loading" }));
    }
  } }]
];
