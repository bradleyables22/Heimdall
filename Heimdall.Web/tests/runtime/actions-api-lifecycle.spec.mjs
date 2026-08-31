import assert from "node:assert/strict";
import {
  actionFetches,
  csrfFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";

async function testPublicApi(page) {
  const api = await page.evaluate(() => ({
    apiVersion: window.Heimdall.apiVersion,
    invoke: typeof window.Heimdall.invoke,
    boot: typeof window.Heimdall.boot,
    onReady: typeof window.Heimdall.onReady,
    clearCsrfToken: typeof window.Heimdall.clearCsrfToken,
    sseConnect: typeof window.Heimdall.sse.connect,
    contentEndpoint: window.Heimdall.config.endpoints.contentActions,
    antiforgery: window.Heimdall.config.antiforgery,
    clientInfo: window.Heimdall.config.clientInfo,
    clientInfoMaxAgeMs: window.Heimdall.config.clientInfoMaxAgeMs,
    requestHeaders: window.Heimdall.config.requestHeaders,
    requestSync: window.Heimdall.config.requestSync,
    requestTimeoutMs: window.Heimdall.config.requestTimeoutMs
  }));

  assert.equal(api.apiVersion, 1);
  assert.equal(api.invoke, "function");
  assert.equal(api.boot, "function");
  assert.equal(api.onReady, "function");
  assert.equal(api.clearCsrfToken, "function");
  assert.equal(api.sseConnect, "function");
  assert.equal(api.contentEndpoint, "/__heimdall/v1/content/actions");
  assert.equal(api.antiforgery, true);
  assert.equal(api.clientInfo, false);
  assert.equal(api.clientInfoMaxAgeMs, 60000);
  assert.equal(api.requestHeaders, null);
  assert.equal(api.requestSync, "parallel");
  assert.equal(api.requestTimeoutMs, 0);
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

async function testRequestLifecycleContract(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="lifecycle-result">Configured</span>' }]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="primary">Primary</div><div id="secondary">Secondary</div>';
    const order = [];
    const snapshots = {};

    document.addEventListener("heimdall:request-config", event => {
      order.push("request-config");
      event.detail.payload = { configured: true };
      event.detail.headers["X-Lifecycle"] = "configured";
      event.detail.target = "#primary";
    });
    document.addEventListener("heimdall:request-before", event => {
      order.push("request-before");
      snapshots.attempt = event.detail.attempt;
      snapshots.header = event.detail.request.headers["X-Lifecycle"];
    });
    document.addEventListener("heimdall:before", () => order.push("before"));
    document.addEventListener("heimdall:swap-before", event => {
      order.push(`swap-before:${event.detail.origin}:${event.detail.kind}`);
      event.detail.target = "#secondary";
    });
    document.addEventListener("heimdall:swap-after", event => {
      order.push(`swap-after:${event.detail.origin}:${event.detail.kind}`);
      snapshots.appliedRootId = event.detail.appliedRoot && event.detail.appliedRoot.id;
    });
    document.addEventListener("heimdall:after", () => order.push("after"));
    document.addEventListener("heimdall:request-after", event => {
      order.push("request-after");
      snapshots.resultOk = event.detail.result.ok;
    });
    document.addEventListener("heimdall:request-finally", event => {
      order.push("request-finally");
      snapshots.finished = event.detail.finishedAt != null;
    });

    const result = await window.Heimdall.invoke("Lifecycle.Configure", { configured: false }, {
      target: "#primary"
    });

    return {
      result: { ok: result.ok },
      order,
      snapshots,
      primaryHtml: document.querySelector("#primary").innerHTML,
      secondaryHtml: document.querySelector("#secondary").innerHTML
    };
  });

  assert.deepEqual(state.order, [
    "request-config",
    "request-before",
    "before",
    "swap-before:action:main",
    "swap-after:action:main",
    "after",
    "request-after",
    "request-finally"
  ]);
  assert.deepEqual(state.snapshots, {
    attempt: 1,
    header: "configured",
    appliedRootId: "lifecycle-result",
    resultOk: true,
    finished: true
  });
  assert.equal(state.primaryHtml, "Primary");
  assert.equal(state.secondaryHtml, '<span id="lifecycle-result">Configured</span>');

  const action = actionFetches(await getFetches(page))[0];
  assert.deepEqual(action.jsonBody, { configured: true });
  assert.equal(action.headers["x-lifecycle"], "configured");
}

export const tests = [
  ["exposes the public API", testPublicApi],
  ["invokes actions with CSRF and swaps HTML", testProgrammaticInvoke],
  ["emits action lifecycle events", testActionLifecycleEvents],
  ["emits mutable and cancellable request lifecycle events", testRequestLifecycleContract]
];
