import assert from "node:assert/strict";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright";
import {
  actionFetches,
  createRuntimePage,
  csrfFetches,
  getFetches,
  installFakeServer
} from "./helpers/runtime-page.mjs";

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const runtimes = [
  {
    name: "bundle",
    path: path.join(projectRoot, "wwwroot", "heimdall-bundle.js")
  },
  {
    name: "minified bundle",
    path: path.join(projectRoot, "wwwroot", "heimdall-bundle.min.js")
  }
];

const tests = [
  ["exposes the public API", testPublicApi],
  ["invokes actions with CSRF and swaps HTML", testProgrammaticInvoke],
  ["emits action lifecycle events", testActionLifecycleEvents],
  ["sanitizes error HTML and calls error callbacks", testActionErrorSanitization],
  ["returns network failures without throwing", testNetworkFailure],
  ["rejects non-serializable payloads with error events", testNonSerializablePayload],
  ["merges custom headers and restores disabled triggers", testCustomHeadersAndDisableState],
  ["handles delegated click triggers", testClickTrigger],
  ["honors delegated trigger scope and ignore boundaries", testDelegatedScopeAndIgnore],
  ["handles delegated keydown triggers", testDelegatedKeydown],
  ["debounces delegated input triggers", testInputDebounce],
  ["cancels delayed hover triggers on mouseout", testHoverDelayCancel],
  ["serializes submit payloads from forms", testSubmitPayload],
  ["resolves explicit payload sources", testExplicitPayloadSources],
  ["resolves closest state payloads", testClosestStatePayload],
  ["applies all swap modes", testSwapModes],
  ["removes outer targets on empty outer swaps", testEmptyOuterSwap],
  ["strips script elements from swapped HTML", testScriptStripping],
  ["processes out-of-band invocations", testOutOfBandInvocation],
  ["invokes JavaScript void directives after swaps", testJsInvokeVoidAfterSwap],
  ["invokes JavaScript void directives before swaps", testJsInvokeVoidBeforeSwap],
  ["emits JavaScript errors for invalid directive paths", testJsInvokeVoidInvalidPath],
  ["does not invoke JavaScript directives when redirecting", testJsInvokeVoidRedirectHardStop],
  ["invokes JavaScript directives on abort responses", testJsInvokeVoidAbortResponse],
  ["preserves this when invoking JavaScript methods", testJsInvokeVoidThisBinding],
  ["invokes JavaScript void directives from SSE messages", testJsInvokeVoidSseMessage],
  ["strips out-of-band invocations with missing targets", testMissingOutOfBandTarget],
  ["honors abort directives while keeping OOB updates", testAbortDirective],
  ["honors redirect directives", testRedirectDirective],
  ["honors redirect text content", testRedirectTextDirective],
  ["navigates on fetch-followed auth redirects", testFetchFollowedAuthRedirect],
  ["strips OOB invocations when OOB is disabled", testOobDisabled],
  ["retries once after suspected CSRF failure", testCsrfRetry],
  ["mints SSE subscribe tokens with CSRF", testSseSubscribeToken],
  ["applies SSE messages and emits message events", testSseMessageSwap],
  ["handles custom SSE events and disconnects", testSseCustomEventAndDisconnect],
  ["shares SSE connections across event subscribers", testSseSharedConnectionAcrossEvents],
  ["retries SSE token failures with backoff", testSseTokenFailureRetry],
  ["reconnects SSE errors with a fresh token", testSseErrorReconnectFreshToken],
  ["pauses SSE reconnects while offline", testSseOfflinePauseResume],
  ["resumes programmatic SSE after hidden pause", testSseProgrammaticHiddenPauseResume],
  ["observes SSE attribute changes", testSseAttributeChanges],
  ["boots root element load triggers", testBootRootLoadTrigger],
  ["boots inserted load triggers with MutationObserver", testMutationObserverBoot]
];

function withTimeout(promise, label, ms = 8000) {
  let timer;
  const timeout = new Promise((_, reject) => {
    timer = setTimeout(() => reject(new Error(`Timed out after ${ms}ms: ${label}`)), ms);
  });

  return Promise.race([promise, timeout]).finally(() => clearTimeout(timer));
}

async function testPublicApi(page) {
  const api = await page.evaluate(() => ({
    apiVersion: window.Heimdall.apiVersion,
    invoke: typeof window.Heimdall.invoke,
    boot: typeof window.Heimdall.boot,
    onReady: typeof window.Heimdall.onReady,
    clearCsrfToken: typeof window.Heimdall.clearCsrfToken,
    sseConnect: typeof window.Heimdall.sse.connect,
    contentEndpoint: window.Heimdall.config.endpoints.contentActions
  }));

  assert.equal(api.apiVersion, 1);
  assert.equal(api.invoke, "function");
  assert.equal(api.boot, "function");
  assert.equal(api.onReady, "function");
  assert.equal(api.clearCsrfToken, "function");
  assert.equal(api.sseConnect, "function");
  assert.equal(api.contentEndpoint, "/__heimdall/v1/content/actions");
}

async function testProgrammaticInvoke(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="updated">Updated</span>' }]
  });

  const result = await page.evaluate(() => {
    document.body.innerHTML = '<div id="target">Old</div>';
    return window.Heimdall.invoke("Notes.Save", { id: 42 }, { target: "#target" });
  });

  assert.equal(result.ok, true);
  assert.equal(await page.locator("#target").innerHTML(), '<span id="updated">Updated</span>');

  const fetches = await getFetches(page);
  const actions = actionFetches(fetches);

  assert.equal(csrfFetches(fetches).length, 1);
  assert.equal(actions.length, 1);
  assert.equal(actions[0].method, "POST");
  assert.equal(new URL(actions[0].url).searchParams.get("action"), "Notes.Save");
  assert.equal(actions[0].headers["x-heimdall-content-action"], "Notes.Save");
  assert.equal(actions[0].headers.requestverificationtoken, "csrf-token");
  assert.deepEqual(actions[0].jsonBody, { id: 42 });
}

async function testActionLifecycleEvents(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="event-updated">Events</span>' }]
  });

  const events = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target">Old</div>';
    const seen = [];

    for (const name of ["heimdall:before", "heimdall:after"]) {
      document.addEventListener(name, ev => {
        seen.push({
          name,
          actionId: ev.detail.actionId,
          payload: ev.detail.payload,
          targetId: ev.detail.target && ev.detail.target.id,
          status: ev.detail.status || null,
          html: ev.detail.html || null
        });
      });
    }

    await window.Heimdall.invoke("Events.Save", { id: 12 }, { target: "#target" });
    return seen;
  });

  assert.equal(events.length, 2);
  assert.equal(events[0].name, "heimdall:before");
  assert.equal(events[0].actionId, "Events.Save");
  assert.deepEqual(events[0].payload, { id: 12 });
  assert.equal(events[0].targetId, "target");
  assert.equal(events[1].name, "heimdall:after");
  assert.equal(events[1].status, 200);
  assert.equal(events[1].html, '<span id="event-updated">Events</span>');
}

async function testActionErrorSanitization(page) {
  await installFakeServer(page, {
    actionResponses: [{
      status: 500,
      body: '<script>window.__bad = true;</script><javascript function="window.App.bad"></javascript><invocation heimdall-content-target="#side"><template>Side</template></invocation><abort reason="x"></abort><redirect url="#bad"></redirect><span id="server-error">Boom</span>'
    }]
  });

  const state = await page.evaluate(async () => {
    window.App = {
      bad() {
        window.__jsBad = true;
      }
    };
    document.body.innerHTML = '<div id="target">Keep</div><div id="side">Side keep</div>';
    const seen = [];
    const result = await window.Heimdall.invoke("Errors.Save", {}, {
      target: "#target",
      onError: errorResult => seen.push({
        ok: errorResult.ok,
        status: errorResult.status,
        error: errorResult.error
      })
    });

    return {
      result,
      seen,
      targetHtml: document.querySelector("#target").innerHTML,
      sideHtml: document.querySelector("#side").innerHTML,
      badRan: window.__bad === true,
      jsBadRan: window.__jsBad === true
    };
  });

  assert.equal(state.result.ok, false);
  assert.equal(state.result.status, 500);
  assert.equal(state.result.html, null);
  assert.equal(state.targetHtml, "Keep");
  assert.equal(state.sideHtml, "Side keep");
  assert.equal(state.badRan, false);
  assert.equal(state.jsBadRan, false);
  assert.equal(state.seen.length, 1);
  assert.equal(state.seen[0].status, 500);
  assert.ok(state.result.error.includes('<span id="server-error">Boom</span>'));
  assert.equal(state.result.error.includes("<script"), false);
  assert.equal(state.result.error.includes("<javascript"), false);
  assert.equal(state.result.error.includes("<invocation"), false);
  assert.equal(state.result.error.includes("<abort"), false);
  assert.equal(state.result.error.includes("<redirect"), false);
}

async function testNetworkFailure(page) {
  await installFakeServer(page);

  const state = await page.evaluate(async () => {
    const originalFetch = window.fetch;
    window.fetch = async (input, init) => {
      const url = typeof input === "string" ? input : input.url;
      if (url.includes("/__heimdall/v1/content/actions")) {
        throw new Error("network down");
      }
      return originalFetch(input, init);
    };

    const errors = [];
    document.addEventListener("heimdall:error", ev => {
      errors.push({
        actionId: ev.detail.actionId,
        message: ev.detail.error && ev.detail.error.message
      });
    });

    document.body.innerHTML = '<div id="target">Keep</div>';
    const result = await window.Heimdall.invoke("Network.Fail", {}, { target: "#target" });

    return {
      result,
      errors,
      targetHtml: document.querySelector("#target").innerHTML
    };
  });

  assert.equal(state.result.ok, false);
  assert.equal(state.result.status, 0);
  assert.equal(state.result.error, "network down");
  assert.equal(state.targetHtml, "Keep");
  assert.equal(state.errors.length, 1);
  assert.equal(state.errors[0].actionId, "Network.Fail");
  assert.equal(state.errors[0].message, "network down");
}

async function testNonSerializablePayload(page) {
  await installFakeServer(page);

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target">Keep</div>';
    const payload = {};
    payload.self = payload;

    const errors = [];
    document.addEventListener("heimdall:error", ev => {
      errors.push({
        actionId: ev.detail.actionId,
        status: ev.detail.status,
        message: ev.detail.error && ev.detail.error.message
      });
    });

    try {
      await window.Heimdall.invoke("Payload.Circular", payload, { target: "#target" });
      return { threw: false, errors };
    } catch (error) {
      return {
        threw: true,
        message: error.message,
        causeName: error.cause && error.cause.name,
        errors
      };
    }
  });

  assert.equal(state.threw, true);
  assert.equal(state.message, "Heimdall payload is not JSON-serializable for action 'Payload.Circular'.");
  assert.equal(state.causeName, "TypeError");
  assert.equal(state.errors.length, 1);
  assert.equal(state.errors[0].actionId, "Payload.Circular");
  assert.equal(state.errors[0].status, 0);
}

async function testCustomHeadersAndDisableState(page) {
  await installFakeServer(page, {
    actionResponses: [
      { body: '<span id="header-done">Headers</span>' },
      { body: '<span id="disable-done">Disabled</span>', delayMs: 40 }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <div id="target">Old</div>
      <button id="save"
              heimdall-content-click="Disable.Save"
              heimdall-content-target="#target">
        Save
      </button>
    `;

    await window.Heimdall.invoke("Headers.Save", {}, {
      target: "#target",
      headers: {
        "X-Custom": "custom-value"
      }
    });

    const button = document.querySelector("#save");
    const beforeState = new Promise(resolve => {
      document.addEventListener("heimdall:before", () => {
        if (button.hasAttribute("disabled")) {
          resolve({
            disabled: button.hasAttribute("disabled"),
            busy: button.getAttribute("aria-busy")
          });
        }
      }, { once: true });
    });

    const afterState = new Promise(resolve => {
      document.addEventListener("heimdall:after", () => {
        setTimeout(() => resolve({
          disabled: button.hasAttribute("disabled"),
          busy: button.getAttribute("aria-busy"),
          targetHtml: document.querySelector("#target").innerHTML
        }), 0);
      }, { once: true });
    });

    button.click();

    return {
      before: await beforeState,
      after: await afterState
    };
  });

  assert.deepEqual(state.before, { disabled: true, busy: "true" });
  assert.deepEqual(state.after, {
    disabled: false,
    busy: null,
    targetHtml: '<span id="disable-done">Disabled</span>'
  });

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 2);
  assert.equal(actions[0].headers["x-custom"], "custom-value");
  assert.equal(actions[0].headers["x-heimdall-content-action"], "Headers.Save");
  assert.equal(actions[1].headers["x-heimdall-content-action"], "Disable.Save");
}

async function testClickTrigger(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<strong id="clicked">Clicked</strong>' }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <button id="save"
              heimdall-content-click="Buttons.Save"
              heimdall-content-target="#target">
        Save
      </button>
      <div id="target">Old</div>
    `;
  });

  await page.click("#save");
  await page.waitForSelector("#clicked");

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 1);
  assert.equal(actions[0].headers["x-heimdall-content-action"], "Buttons.Save");
}

async function testDelegatedScopeAndIgnore(page) {
  await installFakeServer(page, {
    actionResponses: [
      { body: '<span id="self-done">Self</span>' },
      { body: '<span id="inside-done">Inside</span>' }
    ]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <button id="self"
              heimdall-content-click="Scope.Self"
              heimdall-scope="self"
              heimdall-content-target="#target">
        <span id="self-child">Child</span>
      </button>
      <div id="ignored" heimdall-ignore="click">
        <button id="inside"
                heimdall-content-click="Ignore.Inside"
                heimdall-content-target="#target">
          Inside
        </button>
      </div>
      <div id="outer"
           heimdall-content-click="Ignore.Outer"
           heimdall-content-target="#target">
        <span id="outer-ignored" heimdall-ignore="click">Blocked</span>
      </div>
      <div id="target">Old</div>
    `;
  });

  await page.locator("#self-child").evaluate(el => {
    el.dispatchEvent(new MouseEvent("click", { bubbles: true, button: 0 }));
  });
  await page.locator("#self").evaluate(el => {
    el.dispatchEvent(new MouseEvent("click", { bubbles: true, button: 0 }));
  });
  await page.waitForSelector("#self-done");
  await page.click("#inside");
  await page.waitForSelector("#inside-done");
  await page.click("#outer-ignored");

  const actions = actionFetches(await getFetches(page));
  assert.deepEqual(actions.map(action => action.headers["x-heimdall-content-action"]), [
    "Scope.Self",
    "Ignore.Inside"
  ]);
}

async function testDelegatedKeydown(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="key-done">Key</span>' }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <input id="entry"
             heimdall-content-keydown="Keys.Enter"
             heimdall-key="Enter"
             heimdall-content-target="#target">
      <div id="target">Old</div>
    `;
  });

  await page.locator("#entry").press("Escape");
  await page.locator("#entry").press("Enter");
  await page.waitForSelector("#key-done");

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 1);
  assert.equal(actions[0].headers["x-heimdall-content-action"], "Keys.Enter");
}

async function testInputDebounce(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="input-done">Input</span>' }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <form id="filter-form">
        <input id="query"
               name="q"
               value=""
               heimdall-content-input="Search.Query"
               heimdall-payload-from="#filter-form"
               heimdall-content-target="#target"
               heimdall-debounce="80">
      </form>
      <div id="target">Old</div>
    `;

    const input = document.querySelector("#query");
    input.value = "a";
    input.dispatchEvent(new InputEvent("input", { bubbles: true }));
    input.value = "ab";
    input.dispatchEvent(new InputEvent("input", { bubbles: true }));
  });

  await page.waitForSelector("#input-done");

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 1);
  assert.equal(actions[0].headers["x-heimdall-content-action"], "Search.Query");
  assert.deepEqual(actions[0].jsonBody, { q: "ab" });
}

async function testHoverDelayCancel(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="hover-done">Hover</span>' }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <button id="hover"
              heimdall-content-hover="Hover.Show"
              heimdall-hover-delay="80"
              heimdall-content-target="#target">
        Hover
      </button>
      <div id="target">Old</div>
    `;

    const el = document.querySelector("#hover");
    el.dispatchEvent(new MouseEvent("mouseover", { bubbles: true, relatedTarget: document.body }));

    setTimeout(() => {
      el.dispatchEvent(new MouseEvent("mouseout", { bubbles: true, relatedTarget: document.body }));
    }, 20);

    setTimeout(() => {
      el.dispatchEvent(new MouseEvent("mouseover", { bubbles: true, relatedTarget: document.body }));
    }, 140);
  });

  await page.waitForSelector("#hover-done");

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 1);
  assert.equal(actions[0].headers["x-heimdall-content-action"], "Hover.Show");
}

async function testSubmitPayload(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="saved">Saved</span>' }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <form id="form" heimdall-content-submit="Forms.Save" heimdall-content-target="#target">
        <input name="title" value="Hello">
        <input type="checkbox" name="tag" value="a" checked>
        <input type="checkbox" name="tag" value="b" checked>
        <button type="submit">Submit</button>
      </form>
      <div id="target">Old</div>
    `;
  });

  await page.locator("#form").evaluate(form => form.requestSubmit());
  await page.waitForSelector("#saved");

  const actions = actionFetches(await getFetches(page));
  assert.deepEqual(actions[0].jsonBody, { title: "Hello", tag: ["a", "b"] });
}

async function testExplicitPayloadSources(page) {
  await installFakeServer(page, {
    actionResponses: [
      { body: '<span id="static-done">Static</span>' },
      { body: '<span id="self-done">Self</span>' },
      { body: '<span id="ref-done">Ref</span>' }
    ]
  });

  await page.evaluate(() => {
    window.App = {
      Payloads: {
        Selected: {
          id: 99,
          kind: "global"
        }
      }
    };

    document.body.innerHTML = `
      <button id="static"
              heimdall-content-click="Payload.Static"
              heimdall-payload='{"id":7,"kind":"inline"}'
              heimdall-content-target="#target">Static</button>
      <button id="self"
              data-id="8"
              data-kind="dataset"
              heimdall-content-click="Payload.Self"
              heimdall-payload-from="self"
              heimdall-content-target="#target">Self</button>
      <button id="ref"
              heimdall-content-click="Payload.Ref"
              heimdall-payload-ref="App.Payloads.Selected"
              heimdall-content-target="#target">Ref</button>
      <div id="target">Old</div>
    `;
  });

  await page.click("#static");
  await page.waitForSelector("#static-done");
  await page.click("#self");
  await page.waitForSelector("#self-done");
  await page.click("#ref");
  await page.waitForSelector("#ref-done");

  const actions = actionFetches(await getFetches(page));
  assert.deepEqual(actions.map(action => action.jsonBody), [
    { id: 7, kind: "inline" },
    { id: "8", kind: "dataset" },
    { id: 99, kind: "global" }
  ]);
}

async function testClosestStatePayload(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="filtered">Filtered</span>' }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <div data-heimdall-state='{"filter":"open","page":2}'>
        <button id="filter"
                heimdall-content-click="Filters.Apply"
                heimdall-payload-from="closest-state"
                heimdall-content-target="#target">
          Filter
        </button>
      </div>
      <div id="target">Old</div>
    `;
  });

  await page.click("#filter");
  await page.waitForSelector("#filtered");

  const actions = actionFetches(await getFetches(page));
  assert.deepEqual(actions[0].jsonBody, { filter: "open", page: 2 });
}

async function testSwapModes(page) {
  await installFakeServer(page, {
    actionResponses: [
      { body: "<span>A</span>" },
      { body: "<span>B</span>" },
      { body: "<span>C</span>" },
      { body: '<section id="outer">D</section>' },
      { body: "<span>E</span>" }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target">M</div><div id="outer">Old</div><div id="none">Keep</div>';

    await window.Heimdall.invoke("Swap.Inner", {}, { target: "#target", swap: "inner" });
    const afterInner = document.querySelector("#target").innerHTML;

    await window.Heimdall.invoke("Swap.BeforeEnd", {}, { target: "#target", swap: "beforeend" });
    const afterBeforeEnd = document.querySelector("#target").innerHTML;

    await window.Heimdall.invoke("Swap.AfterBegin", {}, { target: "#target", swap: "afterbegin" });
    const afterAfterBegin = document.querySelector("#target").innerHTML;

    await window.Heimdall.invoke("Swap.Outer", {}, { target: "#outer", swap: "outer" });
    const afterOuter = document.querySelector("#outer").outerHTML;

    await window.Heimdall.invoke("Swap.None", {}, { target: "#none", swap: "none" });
    const afterNone = document.querySelector("#none").innerHTML;

    return { afterInner, afterBeforeEnd, afterAfterBegin, afterOuter, afterNone };
  });

  assert.equal(state.afterInner, "<span>A</span>");
  assert.equal(state.afterBeforeEnd, "<span>A</span><span>B</span>");
  assert.equal(state.afterAfterBegin, "<span>C</span><span>A</span><span>B</span>");
  assert.equal(state.afterOuter, '<section id="outer">D</section>');
  assert.equal(state.afterNone, "Keep");
}

async function testEmptyOuterSwap(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: "" }]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<main id="host"><div id="remove">Remove me</div></main>';
    const result = await window.Heimdall.invoke("Swap.Remove", {}, { target: "#remove", swap: "outer" });

    return {
      ok: result.ok,
      html: result.html,
      targetExists: !!document.querySelector("#remove"),
      hostHtml: document.querySelector("#host").innerHTML
    };
  });

  assert.equal(state.ok, true);
  assert.equal(state.html, "");
  assert.equal(state.targetExists, false);
  assert.equal(state.hostHtml, "");
}

async function testScriptStripping(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: '<script>window.__heimdallScriptRan = true;</script><span id="clean">Clean</span>'
    }]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target">Old</div>';
    await window.Heimdall.invoke("Security.StripScripts", {}, { target: "#target" });

    return {
      html: document.querySelector("#target").innerHTML,
      ran: window.__heimdallScriptRan === true,
      scriptCount: document.querySelectorAll("#target script").length
    };
  });

  assert.equal(state.html, '<span id="clean">Clean</span>');
  assert.equal(state.ran, false);
  assert.equal(state.scriptCount, 0);
}

async function testOutOfBandInvocation(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: `
        <invocation heimdall-content-target="#side">
          <template><em id="side-done">Side</em></template>
        </invocation>
        <span id="main-done">Main</span>
      `
    }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = '<div id="main">Old main</div><div id="side">Old side</div>';
    return window.Heimdall.invoke("Oob.Update", {}, { target: "#main" });
  });

  assert.equal(await page.locator("#main").innerHTML(), '\n        \n        <span id="main-done">Main</span>\n      ');
  assert.equal(await page.locator("#side").innerHTML(), '<em id="side-done">Side</em>');
}

async function testMissingOutOfBandTarget(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: `
        <invocation heimdall-content-target="#missing">
          <template><em id="missing-side">Missing</em></template>
        </invocation>
        <span id="main-done">Main</span>
      `
    }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = '<div id="main">Old main</div>';
    return window.Heimdall.invoke("Oob.MissingTarget", {}, { target: "#main" });
  });

  assert.equal(await page.locator("#main").innerHTML(), '\n        \n        <span id="main-done">Main</span>\n      ');
  assert.equal(await page.locator("invocation").count(), 0);
  assert.equal(await page.locator("#missing-side").count(), 0);
}

async function testJsInvokeVoidAfterSwap(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: `
        <javascript function="window.App.record" args='["after","#updated"]'></javascript>
        <span id="updated">Updated</span>
      `
    }]
  });

  const state = await page.evaluate(async () => {
    window.__jsCalls = [];
    window.App = {
      record(label, selector) {
        const el = document.querySelector(selector);
        window.__jsCalls.push({
          label,
          exists: !!el,
          targetHtml: document.querySelector("#target").innerHTML
        });
      }
    };

    document.body.innerHTML = '<div id="target">Old</div>';
    await window.Heimdall.invoke("Js.After", {}, { target: "#target" });

    return {
      targetHtml: document.querySelector("#target").innerHTML,
      calls: window.__jsCalls,
      directiveCount: document.querySelectorAll("javascript").length
    };
  });

  assert.equal(state.targetHtml, '\n        \n        <span id="updated">Updated</span>\n      ');
  assert.deepEqual(state.calls, [{
    label: "after",
    exists: true,
    targetHtml: '\n        \n        <span id="updated">Updated</span>\n      '
  }]);
  assert.equal(state.directiveCount, 0);
}

async function testJsInvokeVoidBeforeSwap(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: `
        <javascript function="window.App.record" timing="before" args='["before","#updated"]'></javascript>
        <span id="updated">Updated</span>
      `
    }]
  });

  const state = await page.evaluate(async () => {
    window.__jsCalls = [];
    window.App = {
      record(label, selector) {
        window.__jsCalls.push({
          label,
          exists: !!document.querySelector(selector),
          targetHtml: document.querySelector("#target").innerHTML
        });
      }
    };

    document.body.innerHTML = '<div id="target">Old</div>';
    await window.Heimdall.invoke("Js.Before", {}, { target: "#target" });

    return {
      targetHtml: document.querySelector("#target").innerHTML,
      calls: window.__jsCalls,
      directiveCount: document.querySelectorAll("javascript").length
    };
  });

  assert.equal(state.targetHtml, '\n        \n        <span id="updated">Updated</span>\n      ');
  assert.deepEqual(state.calls, [{
    label: "before",
    exists: false,
    targetHtml: "Old"
  }]);
  assert.equal(state.directiveCount, 0);
}

async function testJsInvokeVoidInvalidPath(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: '<javascript function="App.record" args=\'["bad"]\'></javascript><span id="done">Done</span>'
    }]
  });

  const state = await page.evaluate(async () => {
    const errors = [];
    document.addEventListener("heimdall:javascript-error", ev => {
      errors.push({
        functionPath: ev.detail.functionPath,
        timing: ev.detail.timing,
        phase: ev.detail.context && ev.detail.context.phase,
        message: ev.detail.error && ev.detail.error.message
      });
    });

    window.App = {
      record() {
        window.__invalidPathRan = true;
      }
    };

    document.body.innerHTML = '<div id="target">Old</div>';
    await window.Heimdall.invoke("Js.Invalid", {}, { target: "#target" });

    return {
      targetHtml: document.querySelector("#target").innerHTML,
      ran: window.__invalidPathRan === true,
      errors,
      directiveCount: document.querySelectorAll("javascript").length
    };
  });

  assert.equal(state.targetHtml, '<span id="done">Done</span>');
  assert.equal(state.ran, false);
  assert.equal(state.directiveCount, 0);
  assert.equal(state.errors.length, 1);
  assert.equal(state.errors[0].functionPath, "App.record");
  assert.equal(state.errors[0].timing, "after");
  assert.equal(state.errors[0].phase, "after");
  assert.ok(state.errors[0].message.includes("must start"));
}

async function testJsInvokeVoidRedirectHardStop(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: '<javascript function="window.App.record" args=\'["redirect"]\'></javascript><redirect url="#js-redirected"></redirect><span>Ignored</span>'
    }]
  });

  const result = await page.evaluate(() => {
    window.__jsCalls = [];
    window.App = {
      record(label) {
        window.__jsCalls.push(label);
      }
    };

    document.body.innerHTML = '<div id="main">Keep</div>';
    return window.Heimdall.invoke("Js.Redirect", {}, { target: "#main" });
  });

  assert.equal(result.redirectUrl, "#js-redirected");
  assert.equal(await page.locator("#main").innerHTML(), "Keep");

  const calls = await page.evaluate(() => window.__jsCalls);
  assert.deepEqual(calls, []);
  assert.equal(new URL(page.url()).hash, "#js-redirected");
}

async function testJsInvokeVoidAbortResponse(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: `
        <abort reason="stay-put"></abort>
        <javascript function="window.App.record" args='["after-abort"]'></javascript>
        <span id="should-not-apply">Nope</span>
      `
    }]
  });

  const state = await page.evaluate(async () => {
    window.__jsCalls = [];
    window.App = {
      record(label) {
        window.__jsCalls.push({
          label,
          targetHtml: document.querySelector("#main").innerHTML
        });
      }
    };

    document.body.innerHTML = '<div id="main">Keep</div>';
    const result = await window.Heimdall.invoke("Js.Abort", {}, { target: "#main" });

    return {
      result,
      calls: window.__jsCalls,
      targetHtml: document.querySelector("#main").innerHTML
    };
  });

  assert.equal(state.result.abortSwap, true);
  assert.equal(state.result.abortReason, "stay-put");
  assert.equal(state.targetHtml, "Keep");
  assert.deepEqual(state.calls, [{
    label: "after-abort",
    targetHtml: "Keep"
  }]);
}

async function testJsInvokeVoidThisBinding(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: '<javascript function="window.App.counter.add" args=\'[3]\'></javascript><span id="done">Done</span>'
    }]
  });

  const state = await page.evaluate(async () => {
    window.App = {
      counter: {
        value: 4,
        add(amount) {
          this.value += amount;
        }
      }
    };

    document.body.innerHTML = '<div id="target">Old</div>';
    await window.Heimdall.invoke("Js.This", {}, { target: "#target" });

    return {
      value: window.App.counter.value,
      targetHtml: document.querySelector("#target").innerHTML
    };
  });

  assert.equal(state.value, 7);
  assert.equal(state.targetHtml, '<span id="done">Done</span>');
}

async function testJsInvokeVoidSseMessage(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-js-sse"],
    bifrostTokens: ["st-js-sse"]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <div id="sse-host"></div>
      <div id="target">Old</div>
    `;

    window.__jsCalls = [];
    window.App = {
      record(selector) {
        window.__jsCalls.push({
          selector,
          exists: !!document.querySelector(selector),
          targetHtml: document.querySelector("#target").innerHTML
        });
      }
    };

    window.__eventSources = [];
    window.EventSource = class {
      constructor(url) {
        this.url = url;
        this.listeners = {};
        this.closed = false;
        window.__eventSources.push(this);
      }

      addEventListener(name, handler) {
        this.listeners[name] = handler;
      }

      close() {
        this.closed = true;
      }
    };

    window.Heimdall.sse.connect("topic:js", {
      element: document.querySelector("#sse-host"),
      target: "#target",
      swap: "inner",
      event: "message"
    });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        const es = window.__eventSources[0];
        if (es && typeof es.onmessage === "function") {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for EventSource message handler"));
        }
      }, 10);
    });

    const es = window.__eventSources[0];
    es.onmessage({
      data: '<javascript function="window.App.record" args=\'["#sse-done"]\'></javascript><span id="sse-done">SSE</span>',
      lastEventId: "js-1"
    });

    return {
      eventSourceUrl: es.url,
      targetHtml: document.querySelector("#target").innerHTML,
      calls: window.__jsCalls,
      directiveCount: document.querySelectorAll("javascript").length
    };
  });

  const eventSourceUrl = new URL(state.eventSourceUrl);
  assert.equal(eventSourceUrl.searchParams.get("topic"), "topic:js");
  assert.equal(eventSourceUrl.searchParams.get("st"), "st-js-sse");
  assert.equal(state.targetHtml, '<span id="sse-done">SSE</span>');
  assert.deepEqual(state.calls, [{
    selector: "#sse-done",
    exists: true,
    targetHtml: '<span id="sse-done">SSE</span>'
  }]);
  assert.equal(state.directiveCount, 0);
}

async function testAbortDirective(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: `
        <abort reason="unchanged"></abort>
        <invocation heimdall-content-target="#side">
          <template><strong id="side-updated">Side</strong></template>
        </invocation>
        <span id="should-not-apply">Nope</span>
      `
    }]
  });

  const result = await page.evaluate(() => {
    document.body.innerHTML = '<div id="main">Keep main</div><div id="side">Old side</div>';
    return window.Heimdall.invoke("Abort.Update", {}, { target: "#main" });
  });

  assert.equal(result.abortSwap, true);
  assert.equal(result.abortReason, "unchanged");
  assert.equal(await page.locator("#main").innerHTML(), "Keep main");
  assert.equal(await page.locator("#side").innerHTML(), '<strong id="side-updated">Side</strong>');
}

async function testRedirectDirective(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<redirect url="#redirected"></redirect><span>Ignored</span>' }]
  });

  const result = await page.evaluate(() => {
    document.body.innerHTML = '<div id="main">Keep</div>';
    return window.Heimdall.invoke("Redirect.Go", {}, { target: "#main" });
  });

  assert.equal(result.redirectUrl, "#redirected");
  assert.equal(result.abortSwap, true);
  assert.equal(await page.locator("#main").innerHTML(), "Keep");
  assert.equal(new URL(page.url()).hash, "#redirected");
}

async function testRedirectTextDirective(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<redirect>#text-redirected</redirect><span>Ignored</span>' }]
  });

  const result = await page.evaluate(() => {
    document.body.innerHTML = '<div id="main">Keep</div>';
    return window.Heimdall.invoke("Redirect.Text", {}, { target: "#main" });
  });

  assert.equal(result.redirectUrl, "#text-redirected");
  assert.equal(result.abortReason, "redirect");
  assert.equal(await page.locator("#main").innerHTML(), "Keep");
  assert.equal(new URL(page.url()).hash, "#text-redirected");
}

async function testFetchFollowedAuthRedirect(page) {
  const signInUrl = "http://heimdall.test/signin?ReturnUrl=%2F__heimdall%2Fv1%2Fcontent%2Factions";

  await installFakeServer(page, {
    actionResponses: [{
      body: '<form id="login-form">Sign in</form>',
      redirected: true,
      url: signInUrl
    }]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="main">Secure content</div>';
    const result = await window.Heimdall.invoke("Secure.Refresh", {}, { target: "#main" });

    return {
      result: {
        ok: result.ok,
        status: result.status,
        redirectUrl: result.redirectUrl,
        abortSwap: result.abortSwap,
        abortReason: result.abortReason
      },
      targetHtml: document.querySelector("#main").innerHTML
    };
  });

  await page.waitForURL(signInUrl);

  assert.deepEqual(state.result, {
    ok: true,
    status: 200,
    redirectUrl: signInUrl,
    abortSwap: true,
    abortReason: "redirect"
  });
  assert.equal(state.targetHtml, "Secure content");
  assert.equal(page.url(), signInUrl);
}

async function testOobDisabled(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: `
        <invocation heimdall-content-target="#side">
          <template><em id="side-should-not-change">Side</em></template>
        </invocation>
        <span id="main-done">Main</span>
      `
    }]
  });

  await page.evaluate(() => {
    window.Heimdall.config.oobEnabled = false;
    document.body.innerHTML = '<div id="main">Old main</div><div id="side">Old side</div>';
    return window.Heimdall.invoke("Oob.Disabled", {}, { target: "#main" });
  });

  assert.equal(await page.locator("#main #main-done").count(), 1);
  assert.equal(await page.locator("#main invocation").count(), 0);
  assert.equal(await page.locator("#side").innerHTML(), "Old side");
  assert.equal(await page.locator("#side-should-not-change").count(), 0);
}

async function testCsrfRetry(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-1", "csrf-2"],
    actionResponses: [
      { status: 400, body: "csrf validation failed" },
      { status: 200, body: '<span id="retry-ok">Retried</span>' }
    ]
  });

  const result = await page.evaluate(() => {
    document.body.innerHTML = '<div id="target">Old</div>';
    return window.Heimdall.invoke("Retry.Save", {}, { target: "#target" });
  });

  assert.equal(result.ok, true);
  assert.equal(await page.locator("#target").innerHTML(), '<span id="retry-ok">Retried</span>');

  const fetches = await getFetches(page);
  const actions = actionFetches(fetches);

  assert.equal(csrfFetches(fetches).length, 2);
  assert.equal(actions.length, 2);
  assert.equal(actions[0].headers.requestverificationtoken, "csrf-1");
  assert.equal(actions[1].headers.requestverificationtoken, "csrf-2");
}

async function testSseSubscribeToken(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-sse"],
    bifrostTokens: ["st-123"]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="sse-host"></div>';
    window.__eventSourceUrls = [];

    window.EventSource = class {
      constructor(url) {
        this.url = url;
        window.__eventSourceUrls.push(url);
      }

      addEventListener() {
      }

      close() {
      }
    };

    window.Heimdall.sse.connect("topic:one", {
      element: document.querySelector("#sse-host")
    });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        if (window.__eventSourceUrls.length > 0) {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for EventSource"));
        }
      }, 10);
    });

    return {
      eventSourceUrl: window.__eventSourceUrls[0]
    };
  });

  const eventSourceUrl = new URL(state.eventSourceUrl);
  assert.equal(eventSourceUrl.pathname, "/__heimdall/v1/bifrost");
  assert.equal(eventSourceUrl.searchParams.get("topic"), "topic:one");
  assert.equal(eventSourceUrl.searchParams.get("st"), "st-123");

  const fetches = await getFetches(page);
  const tokenFetch = fetches.find(fetch => fetch.url.includes("/__heimdall/v1/bifrost/token"));

  assert.equal(csrfFetches(fetches).length, 1);
  assert.ok(tokenFetch);
  assert.equal(new URL(tokenFetch.url).searchParams.get("topic"), "topic:one");
  assert.equal(tokenFetch.headers.requestverificationtoken, "csrf-sse");
}

async function testSseMessageSwap(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-sse-message"],
    bifrostTokens: ["st-message"]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <div id="sse-host"></div>
      <div id="target">Old</div>
    `;

    window.__eventSources = [];
    window.EventSource = class {
      constructor(url) {
        this.url = url;
        this.listeners = {};
        window.__eventSources.push(this);
      }

      addEventListener(name, handler) {
        this.listeners[name] = handler;
      }

      close() {
        this.closed = true;
      }
    };

    const seen = [];
    document.addEventListener("heimdall:sse-message", ev => {
      seen.push({
        topic: ev.detail.topic,
        id: ev.detail.id,
        bytes: ev.detail.bytes,
        targetHtml: document.querySelector("#target").innerHTML
      });
    });

    window.Heimdall.sse.connect("topic:message", {
      element: document.querySelector("#sse-host"),
      target: "#target",
      swap: "inner",
      event: "message"
    });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        const es = window.__eventSources[0];
        if (es && typeof es.onmessage === "function") {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for EventSource message handler"));
        }
      }, 10);
    });

    const es = window.__eventSources[0];
    es.onmessage({
      data: '<span id="sse-done">SSE</span>',
      lastEventId: "evt-1"
    });

    return {
      targetHtml: document.querySelector("#target").innerHTML,
      eventSourceUrl: es.url,
      seen
    };
  });

  const eventSourceUrl = new URL(state.eventSourceUrl);
  assert.equal(eventSourceUrl.searchParams.get("topic"), "topic:message");
  assert.equal(eventSourceUrl.searchParams.get("st"), "st-message");
  assert.equal(state.targetHtml, '<span id="sse-done">SSE</span>');
  assert.equal(state.seen.length, 1);
  assert.equal(state.seen[0].topic, "topic:message");
  assert.equal(state.seen[0].id, "evt-1");
  assert.equal(state.seen[0].bytes, '<span id="sse-done">SSE</span>'.length);
  assert.equal(state.seen[0].targetHtml, '<span id="sse-done">SSE</span>');
}

async function testSseCustomEventAndDisconnect(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-sse-custom"],
    bifrostTokens: ["st-custom"]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <div id="sse-host"></div>
      <div id="target">Old</div>
    `;

    window.__eventSources = [];
    window.EventSource = class {
      constructor(url) {
        this.url = url;
        this.listeners = {};
        this.closed = false;
        window.__eventSources.push(this);
      }

      addEventListener(name, handler) {
        this.listeners[name] = handler;
      }

      close() {
        this.closed = true;
      }
    };

    const closeEvents = [];
    document.addEventListener("heimdall:sse-close", ev => {
      closeEvents.push({
        topic: ev.detail.topic,
        reason: ev.detail.reason
      });
    });

    const handle = window.Heimdall.sse.connect("topic:custom", {
      element: document.querySelector("#sse-host"),
      target: "#target",
      swap: "inner",
      event: "custom-event"
    });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        const es = window.__eventSources[0];
        if (es && typeof es.listeners["custom-event"] === "function") {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for custom EventSource handler"));
        }
      }, 10);
    });

    const es = window.__eventSources[0];
    es.listeners["custom-event"]({
      data: '<strong id="custom-done">Custom</strong>',
      lastEventId: "custom-1"
    });

    handle.close();

    return {
      eventSourceUrl: es.url,
      closed: es.closed,
      targetHtml: document.querySelector("#target").innerHTML,
      handleTopic: handle.topic,
      handleUrl: handle.url,
      closeEvents
    };
  });

  const eventSourceUrl = new URL(state.eventSourceUrl);
  assert.equal(eventSourceUrl.searchParams.get("topic"), "topic:custom");
  assert.equal(eventSourceUrl.searchParams.get("st"), "st-custom");
  assert.equal(state.targetHtml, '<strong id="custom-done">Custom</strong>');
  assert.equal(state.handleTopic, "topic:custom");
  assert.equal(state.handleUrl, state.eventSourceUrl);
  assert.equal(state.closed, true);
  assert.deepEqual(state.closeEvents, [{ topic: "topic:custom", reason: "manual" }]);
}

async function testSseSharedConnectionAcrossEvents(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-sse-shared"],
    bifrostTokens: ["st-shared"]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <div id="sse-host-a"></div>
      <div id="sse-host-b"></div>
      <div id="target-a">A old</div>
      <div id="target-b">B old</div>
    `;

    window.__eventSources = [];
    window.EventSource = class {
      constructor(url) {
        this.url = url;
        this.listeners = {};
        this.closed = false;
        window.__eventSources.push(this);
      }

      addEventListener(name, handler) {
        this.listeners[name] = handler;
      }

      removeEventListener(name, handler) {
        if (this.listeners[name] === handler) {
          delete this.listeners[name];
        }
      }

      close() {
        this.closed = true;
      }
    };

    const messages = [];
    const closeEvents = [];

    document.addEventListener("heimdall:sse-message", ev => {
      messages.push({
        topic: ev.detail.topic,
        event: ev.detail.event,
        targetId: ev.detail.el && ev.detail.el.id
      });
    });

    document.addEventListener("heimdall:sse-close", ev => {
      closeEvents.push({
        topic: ev.detail.topic,
        reason: ev.detail.reason,
        el: ev.detail.el && ev.detail.el.id
      });
    });

    const first = window.Heimdall.sse.connect("topic:shared", {
      element: document.querySelector("#sse-host-a"),
      target: "#target-a",
      swap: "inner",
      event: "order.updated"
    });

    const second = window.Heimdall.sse.connect("topic:shared", {
      element: document.querySelector("#sse-host-b"),
      target: "#target-b",
      swap: "inner",
      event: "toast"
    });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        const es = window.__eventSources[0];
        if (
          window.__eventSources.length === 1 &&
          es &&
          typeof es.listeners["order.updated"] === "function" &&
          typeof es.listeners.toast === "function"
        ) {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for shared EventSource listeners"));
        }
      }, 10);
    });

    const es = window.__eventSources[0];
    es.listeners["order.updated"]({
      data: '<span id="order-done">Order</span>',
      lastEventId: "order-1"
    });

    first.close();
    const closedAfterFirst = es.closed;

    es.listeners.toast({
      data: '<strong id="toast-done">Toast</strong>',
      lastEventId: "toast-1"
    });

    second.close();

    return {
      eventSourceCount: window.__eventSources.length,
      eventSourceUrl: es.url,
      firstUrl: first.url,
      secondUrl: second.url,
      closedAfterFirst,
      closedAfterSecond: es.closed,
      targetA: document.querySelector("#target-a").innerHTML,
      targetB: document.querySelector("#target-b").innerHTML,
      messages,
      closeEvents
    };
  });

  const eventSourceUrl = new URL(state.eventSourceUrl);

  assert.equal(state.eventSourceCount, 1);
  assert.equal(eventSourceUrl.searchParams.get("topic"), "topic:shared");
  assert.equal(eventSourceUrl.searchParams.get("st"), "st-shared");
  assert.equal(state.firstUrl, state.eventSourceUrl);
  assert.equal(state.secondUrl, state.eventSourceUrl);
  assert.equal(state.closedAfterFirst, false);
  assert.equal(state.closedAfterSecond, true);
  assert.equal(state.targetA, '<span id="order-done">Order</span>');
  assert.equal(state.targetB, '<strong id="toast-done">Toast</strong>');
  assert.deepEqual(state.messages, [
    { topic: "topic:shared", event: "order.updated", targetId: "sse-host-a" },
    { topic: "topic:shared", event: "toast", targetId: "sse-host-b" }
  ]);
  assert.deepEqual(state.closeEvents, [
    { topic: "topic:shared", reason: "manual", el: "sse-host-a" },
    { topic: "topic:shared", reason: "manual", el: "sse-host-b" }
  ]);

  const fetches = await getFetches(page);
  const tokenFetches = fetches.filter(fetch => fetch.url.includes("/__heimdall/v1/bifrost/token"));
  assert.equal(tokenFetches.length, 1);
}

async function testSseTokenFailureRetry(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-sse-retry"],
    bifrostTokenResponses: [
      { status: 401, body: "try again" },
      { token: "st-retry" }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="sse-host"></div>';
    window.Heimdall.config.sseReconnectDelayMs = 10;
    window.Heimdall.config.sseReconnectMaxDelayMs = 10;

    window.__eventSources = [];
    window.EventSource = class {
      constructor(url) {
        this.url = url;
        this.closed = false;
        window.__eventSources.push(this);
      }

      addEventListener() {
      }

      close() {
        this.closed = true;
      }
    };

    const scheduled = [];
    document.addEventListener("heimdall:sse-reconnect-scheduled", ev => {
      scheduled.push({
        topic: ev.detail.topic,
        reason: ev.detail.reason,
        status: ev.detail.status,
        delayMs: ev.detail.delayMs
      });
    });

    window.Heimdall.sse.connect("topic:retry", {
      element: document.querySelector("#sse-host"),
      event: "message"
    });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        if (window.__eventSources.length > 0) {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for retry EventSource"));
        }
      }, 10);
    });

    return {
      eventSourceUrl: window.__eventSources[0].url,
      scheduled
    };
  });

  const eventSourceUrl = new URL(state.eventSourceUrl);
  assert.equal(eventSourceUrl.searchParams.get("topic"), "topic:retry");
  assert.equal(eventSourceUrl.searchParams.get("st"), "st-retry");
  assert.equal(state.scheduled.length, 1);
  assert.deepEqual(state.scheduled[0], {
    topic: "topic:retry",
    reason: "token-failed",
    status: 401,
    delayMs: 10
  });

  const fetches = await getFetches(page);
  const tokenFetches = fetches.filter(fetch => fetch.url.includes("/__heimdall/v1/bifrost/token"));
  assert.equal(tokenFetches.length, 2);
}

async function testSseErrorReconnectFreshToken(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-sse-error"],
    bifrostTokenResponses: [
      { token: "st-first" },
      { token: "st-second" }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="sse-host"></div>';
    window.Heimdall.config.sseReconnectDelayMs = 10;
    window.Heimdall.config.sseReconnectMaxDelayMs = 10;

    window.__eventSources = [];
    window.EventSource = class {
      constructor(url) {
        this.url = url;
        this.closed = false;
        window.__eventSources.push(this);
      }

      addEventListener() {
      }

      close() {
        this.closed = true;
      }
    };

    const scheduled = [];
    document.addEventListener("heimdall:sse-reconnect-scheduled", ev => {
      scheduled.push({
        topic: ev.detail.topic,
        reason: ev.detail.reason,
        delayMs: ev.detail.delayMs
      });
    });

    window.Heimdall.sse.connect("topic:error", {
      element: document.querySelector("#sse-host"),
      event: "message"
    });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        const es = window.__eventSources[0];
        if (es && typeof es.onerror === "function") {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for initial EventSource"));
        }
      }, 10);
    });

    const first = window.__eventSources[0];
    first.onerror({ type: "error" });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        if (window.__eventSources.length > 1) {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for reconnect EventSource"));
        }
      }, 10);
    });

    return {
      firstUrl: window.__eventSources[0].url,
      secondUrl: window.__eventSources[1].url,
      firstClosed: window.__eventSources[0].closed,
      scheduled
    };
  });

  const firstUrl = new URL(state.firstUrl);
  const secondUrl = new URL(state.secondUrl);

  assert.equal(firstUrl.searchParams.get("st"), "st-first");
  assert.equal(secondUrl.searchParams.get("st"), "st-second");
  assert.equal(state.firstClosed, true);
  assert.equal(state.scheduled.length, 1);
  assert.deepEqual(state.scheduled[0], {
    topic: "topic:error",
    reason: "eventsource-error",
    delayMs: 10
  });

  const fetches = await getFetches(page);
  const tokenFetches = fetches.filter(fetch => fetch.url.includes("/__heimdall/v1/bifrost/token"));
  assert.equal(tokenFetches.length, 2);
}

async function testSseOfflinePauseResume(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-sse-offline"],
    bifrostTokenResponses: [
      { token: "st-offline-first" },
      { token: "st-offline-second" }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="sse-host"></div>';
    window.Heimdall.config.sseReconnectDelayMs = 10;
    window.Heimdall.config.sseReconnectMaxDelayMs = 10;
    window.__heimdallOnline = true;

    Object.defineProperty(window.navigator, "onLine", {
      configurable: true,
      get: () => window.__heimdallOnline
    });

    window.__eventSources = [];
    window.EventSource = class {
      constructor(url) {
        this.url = url;
        this.closed = false;
        window.__eventSources.push(this);
      }

      addEventListener() {
      }

      close() {
        this.closed = true;
      }
    };

    const paused = [];
    const resumed = [];
    const scheduled = [];

    document.addEventListener("heimdall:sse-pause", ev => {
      paused.push({
        topic: ev.detail.topic,
        reason: ev.detail.reason
      });
    });

    document.addEventListener("heimdall:sse-resume", ev => {
      resumed.push({
        topic: ev.detail.topic,
        reason: ev.detail.reason,
        previousReason: ev.detail.previousReason
      });
    });

    document.addEventListener("heimdall:sse-reconnect-scheduled", ev => {
      scheduled.push({
        topic: ev.detail.topic,
        reason: ev.detail.reason
      });
    });

    window.Heimdall.sse.connect("topic:offline", {
      element: document.querySelector("#sse-host"),
      event: "message"
    });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        const es = window.__eventSources[0];
        if (es && typeof es.onerror === "function") {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for initial EventSource"));
        }
      }, 10);
    });

    window.__heimdallOnline = false;
    window.__eventSources[0].onerror({ type: "error" });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        if (paused.length > 0 && window.__eventSources[0].closed) {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for offline pause"));
        }
      }, 10);
    });

    await new Promise(resolve => setTimeout(resolve, 50));
    const countWhileOffline = window.__eventSources.length;

    window.__heimdallOnline = true;
    window.dispatchEvent(new Event("online"));

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        if (window.__eventSources.length > 1) {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for online resume"));
        }
      }, 10);
    });

    return {
      firstUrl: window.__eventSources[0].url,
      secondUrl: window.__eventSources[1].url,
      firstClosed: window.__eventSources[0].closed,
      countWhileOffline,
      paused,
      resumed,
      scheduled
    };
  });

  const firstUrl = new URL(state.firstUrl);
  const secondUrl = new URL(state.secondUrl);

  assert.equal(firstUrl.searchParams.get("st"), "st-offline-first");
  assert.equal(secondUrl.searchParams.get("st"), "st-offline-second");
  assert.equal(state.firstClosed, true);
  assert.equal(state.countWhileOffline, 1);
  assert.deepEqual(state.paused, [{ topic: "topic:offline", reason: "offline" }]);
  assert.deepEqual(state.resumed, [{ topic: "topic:offline", reason: "online", previousReason: "offline" }]);
  assert.deepEqual(state.scheduled, []);

  const fetches = await getFetches(page);
  const tokenFetches = fetches.filter(fetch => fetch.url.includes("/__heimdall/v1/bifrost/token"));
  assert.equal(tokenFetches.length, 2);
}

async function testSseProgrammaticHiddenPauseResume(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-sse-hidden"],
    bifrostTokenResponses: [
      { token: "st-hidden" }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="sse-host"></div>';
    window.Heimdall.config.ssePauseWhenHidden = true;
    window.__heimdallHidden = false;

    Object.defineProperty(document, "hidden", {
      configurable: true,
      get: () => window.__heimdallHidden
    });

    window.__eventSources = [];
    window.EventSource = class {
      constructor(url) {
        this.url = url;
        this.closed = false;
        window.__eventSources.push(this);
      }

      addEventListener() {
      }

      close() {
        this.closed = true;
      }
    };

    const paused = [];
    const resumed = [];

    document.addEventListener("heimdall:sse-pause", ev => {
      paused.push({
        topic: ev.detail.topic,
        reason: ev.detail.reason
      });
    });

    document.addEventListener("heimdall:sse-resume", ev => {
      resumed.push({
        topic: ev.detail.topic,
        reason: ev.detail.reason,
        previousReason: ev.detail.previousReason
      });
    });

    const handle = window.Heimdall.sse.connect("topic:hidden", {
      element: document.querySelector("#sse-host"),
      event: "message"
    });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        if (window.__eventSources.length > 0) {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for initial EventSource"));
        }
      }, 10);
    });

    window.__heimdallHidden = true;
    document.dispatchEvent(new Event("visibilitychange"));

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        if (window.__eventSources[0].closed && paused.length > 0) {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for hidden pause"));
        }
      }, 10);
    });

    window.__heimdallHidden = false;
    document.dispatchEvent(new Event("visibilitychange"));

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        if (window.__eventSources.length > 1) {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for visible resume"));
        }
      }, 10);
    });

    return {
      firstUrl: window.__eventSources[0].url,
      secondUrl: window.__eventSources[1].url,
      firstClosed: window.__eventSources[0].closed,
      handleTopic: handle.topic,
      handleUrl: handle.url,
      paused,
      resumed
    };
  });

  const firstUrl = new URL(state.firstUrl);
  const secondUrl = new URL(state.secondUrl);

  assert.equal(firstUrl.searchParams.get("topic"), "topic:hidden");
  assert.equal(secondUrl.searchParams.get("topic"), "topic:hidden");
  assert.equal(firstUrl.searchParams.get("st"), "st-hidden");
  assert.equal(secondUrl.searchParams.get("st"), "st-hidden");
  assert.equal(state.firstClosed, true);
  assert.equal(state.handleTopic, "topic:hidden");
  assert.equal(state.handleUrl, state.secondUrl);
  assert.deepEqual(state.paused, [{ topic: "topic:hidden", reason: "hidden" }]);
  assert.deepEqual(state.resumed, [{ topic: "topic:hidden", reason: "visible", previousReason: "hidden" }]);

  const fetches = await getFetches(page);
  const tokenFetches = fetches.filter(fetch => fetch.url.includes("/__heimdall/v1/bifrost/token"));
  assert.equal(tokenFetches.length, 1);
}

async function testSseAttributeChanges(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-sse-attrs"],
    bifrostTokenResponses: [
      { token: "st-attrs-one" },
      { token: "st-attrs-two" }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="sse-host"></div><div id="target">Old</div>';
    window.Heimdall.config.sseReconnectDelayMs = 10;
    window.Heimdall.config.sseReconnectMaxDelayMs = 10;

    window.__eventSources = [];
    window.EventSource = class {
      constructor(url) {
        this.url = url;
        this.closed = false;
        window.__eventSources.push(this);
      }

      addEventListener() {
      }

      close() {
        this.closed = true;
      }
    };

    const closeEvents = [];
    document.addEventListener("heimdall:sse-close", ev => {
      closeEvents.push({
        topic: ev.detail.topic,
        reason: ev.detail.reason
      });
    });

    const host = document.querySelector("#sse-host");
    host.setAttribute("heimdall-sse", "topic:attrs");
    host.setAttribute("heimdall-sse-target", "#target");
    host.setAttribute("heimdall-sse-swap", "inner");
    host.setAttribute("heimdall-sse-event", "message");

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        if (window.__eventSources.length > 0) {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for attribute-created EventSource"));
        }
      }, 10);
    });

    host.setAttribute("heimdall-sse", "topic:attrs-next");

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        if (window.__eventSources.length > 1) {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for attribute-changed EventSource"));
        }
      }, 10);
    });

    host.setAttribute("heimdall-sse-disable", "true");

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        if (window.__eventSources[1] && window.__eventSources[1].closed) {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for SSE disable close"));
        }
      }, 10);
    });

    return {
      firstUrl: window.__eventSources[0].url,
      secondUrl: window.__eventSources[1].url,
      firstClosed: window.__eventSources[0].closed,
      secondClosed: window.__eventSources[1].closed,
      closeEvents
    };
  });

  const firstUrl = new URL(state.firstUrl);
  const secondUrl = new URL(state.secondUrl);

  assert.equal(firstUrl.searchParams.get("topic"), "topic:attrs");
  assert.equal(firstUrl.searchParams.get("st"), "st-attrs-one");
  assert.equal(secondUrl.searchParams.get("topic"), "topic:attrs-next");
  assert.equal(secondUrl.searchParams.get("st"), "st-attrs-two");
  assert.equal(state.firstClosed, true);
  assert.equal(state.secondClosed, true);
  assert.deepEqual(state.closeEvents, [
    { topic: "topic:attrs", reason: "topic-changed" },
    { topic: "topic:attrs-next", reason: "disabled" }
  ]);
}

async function testBootRootLoadTrigger(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="root-loaded">Root</span>' }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <div id="load-root"
           heimdall-content-load="Load.Root"
           heimdall-content-target="#target"></div>
      <div id="target">Old</div>
    `;

    window.Heimdall.boot(document.querySelector("#load-root"));
  });

  await page.waitForSelector("#root-loaded");

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 1);
  assert.equal(actions[0].headers["x-heimdall-content-action"], "Load.Root");
}

async function testMutationObserverBoot(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="loaded">Loaded</span>' }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = '<main id="host"></main>';
    document.querySelector("#host").insertAdjacentHTML(
      "beforeend",
      '<div id="load-target" heimdall-content-load="Load.Dynamic"></div>'
    );
  });

  await page.waitForSelector("#loaded");

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 1);
  assert.equal(actions[0].headers["x-heimdall-content-action"], "Load.Dynamic");
}

let failures = 0;
const browser = await chromium.launch();

try {
  for (const runtime of runtimes) {
    for (const [name, test] of tests) {
      const label = `${runtime.name}: ${name}`;
      let page = null;

      try {
        page = await withTimeout(createRuntimePage(browser, runtime), `${label} setup`);
        await withTimeout(test(page), label);
        console.log(`ok - ${label}`);
      } catch (error) {
        failures += 1;
        console.error(`not ok - ${label}`);
        console.error(error);
      } finally {
        if (page) {
          await page.close();
        }
      }
    }
  }
} finally {
  await browser.close();
}

if (failures > 0) {
  throw new Error(`${failures} Heimdall runtime test(s) failed.`);
}

console.log(`Heimdall runtime tests passed (${runtimes.length * tests.length} checks).`);
