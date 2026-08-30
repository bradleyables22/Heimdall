import assert from "node:assert/strict";
import { installFakeServer } from "../helpers/runtime-page.mjs";

async function testPushNormalizesRootRelativeUrls(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: '<span id="updated">Updated</span><history mode="push" url="orders/42?tab=activity#notes"></history>'
    }]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target">Old</div>';
    const initialLength = history.length;
    const events = [];
    document.addEventListener("heimdall:history-before", event => {
      events.push(`before:${location.pathname}:${document.querySelector("#updated") != null}`);
    });
    document.addEventListener("heimdall:history-after", event => {
      events.push(`after:${event.detail.mode}:${event.detail.url}`);
    });

    const result = await window.Heimdall.invoke("Orders.Open", {}, { target: "#target" });
    return {
      path: `${location.pathname}${location.search}${location.hash}`,
      initialLength,
      finalLength: history.length,
      targetHtml: document.querySelector("#target").innerHTML,
      directiveCount: document.querySelectorAll("history").length,
      history: result.history && {
        applied: result.history.applied,
        mode: result.history.mode,
        url: result.history.url
      },
      events
    };
  });

  assert.equal(state.path, "/orders/42?tab=activity#notes");
  assert.equal(state.finalLength, state.initialLength + 1);
  assert.equal(state.targetHtml, '<span id="updated">Updated</span>');
  assert.equal(state.directiveCount, 0);
  assert.deepEqual(state.history, { applied: true, mode: "push", url: "/orders/42?tab=activity#notes" });
  assert.deepEqual(state.events, ["before:/:true", "after:push:/orders/42?tab=activity#notes"]);
}

async function testLeadingSlashAndRootlessUrlsAreEquivalent(page) {
  await installFakeServer(page, {
    actionResponses: [
      { body: '<history mode="replace" url="orders/42"></history>' },
      { body: '<history mode="replace" url="/orders/42"></history>' }
    ]
  });

  const state = await page.evaluate(async () => {
    const first = await window.Heimdall.invoke("History.Rootless", {}, { swap: "none" });
    const firstPath = location.pathname;
    history.replaceState(history.state, "", "/starting-point");
    const second = await window.Heimdall.invoke("History.Rooted", {}, { swap: "none" });
    return {
      firstPath,
      secondPath: location.pathname,
      firstUrl: first.history.url,
      secondUrl: second.history.url
    };
  });

  assert.deepEqual(state, {
    firstPath: "/orders/42",
    secondPath: "/orders/42",
    firstUrl: "/orders/42",
    secondUrl: "/orders/42"
  });
}

async function testReplaceAndSameOriginUrlForms(page) {
  await installFakeServer(page, {
    actionResponses: [
      { body: '<history mode="replace" url="?filter=open#results"></history>' },
      { body: '<history mode="replace" url="http://heimdall.test/orders/7?from=absolute"></history>' }
    ]
  });

  const state = await page.evaluate(async () => {
    history.replaceState({ applicationValue: 7 }, "", "/orders");
    const initialLength = history.length;
    await window.Heimdall.invoke("History.Query", {}, { swap: "none" });
    const queryPath = `${location.pathname}${location.search}${location.hash}`;
    await window.Heimdall.invoke("History.Absolute", {}, { swap: "none" });
    return {
      initialLength,
      finalLength: history.length,
      queryPath,
      absolutePath: `${location.pathname}${location.search}${location.hash}`,
      applicationValue: history.state.applicationValue
    };
  });

  assert.equal(state.finalLength, state.initialLength);
  assert.equal(state.queryPath, "/orders?filter=open#results");
  assert.equal(state.absolutePath, "/orders/7?from=absolute");
  assert.equal(state.applicationValue, 7, "History replacement should preserve existing object state fields.");
}

async function testSseStripsHistoryWithoutChangingUrl(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-history-sse"],
    bifrostTokens: ["st-history-sse"]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="sse-host"></div><div id="target">Old</div>';
    window.__eventSources = [];
    window.EventSource = class {
      constructor(url) {
        this.url = url;
        this.listeners = {};
        window.__eventSources.push(this);
      }
      addEventListener(name, handler) { this.listeners[name] = handler; }
      close() { this.closed = true; }
    };

    const historyEvents = [];
    document.addEventListener("heimdall:history-after", event => historyEvents.push(event.detail.url));
    document.addEventListener("heimdall:history-error", event => historyEvents.push(event.detail.error.message));

    window.Heimdall.sse.connect("topic:history", {
      element: document.querySelector("#sse-host"),
      target: "#target",
      swap: "inner",
      event: "message"
    });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        const source = window.__eventSources[0];
        if (source && typeof source.onmessage === "function") {
          clearInterval(timer);
          resolve();
        } else if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for history EventSource"));
        }
      }, 10);
    });

    window.__eventSources[0].onmessage({
      data: '<history mode="push" url="sse-ignored"></history><span id="sse-history-result">Updated</span>',
      lastEventId: "history-1"
    });

    return {
      path: location.pathname,
      targetHtml: document.querySelector("#target").innerHTML,
      directiveCount: document.querySelectorAll("history").length,
      historyEvents
    };
  });

  assert.deepEqual(state, {
    path: "/",
    targetHtml: '<span id="sse-history-result">Updated</span>',
    directiveCount: 0,
    historyEvents: []
  });
}

async function testHistoryLifecycleCanModifyOrCancel(page) {
  await installFakeServer(page, {
    actionResponses: [
      { body: '<history mode="push" url="ignored"></history>' },
      { body: '<history mode="push" url="cancelled"></history>' }
    ]
  });

  const state = await page.evaluate(async () => {
    const modify = event => {
      event.detail.mode = "replace";
      event.detail.url = "customized";
    };
    document.addEventListener("heimdall:history-before", modify, { once: true });
    const modified = await window.Heimdall.invoke("History.Modify", {}, { swap: "none" });

    document.addEventListener("heimdall:history-before", event => event.preventDefault(), { once: true });
    const cancelled = await window.Heimdall.invoke("History.Cancel", {}, { swap: "none" });
    return {
      path: location.pathname,
      modified: {
        applied: modified.history.applied,
        mode: modified.history.mode,
        url: modified.history.url
      },
      cancelled: {
        applied: cancelled.history.applied,
        cancelled: cancelled.history.cancelled
      }
    };
  });

  assert.deepEqual(state, {
    path: "/customized",
    modified: { applied: true, mode: "replace", url: "/customized" },
    cancelled: { applied: false, cancelled: true }
  });
}

async function testInvalidHistoryIsStrippedAndReported(page) {
  const invalidBodies = [
    '<span>Cross origin</span><history mode="push" url="https://example.com/orders"></history>',
    '<span>Protocol relative</span><history mode="push" url="//example.com/orders"></history>',
    '<span>Backslash</span><history mode="push" url="orders\\42"></history>',
    '<span>Mode</span><history mode="pop" url="orders"></history>',
    '<span>Missing</span><history mode="push"></history>',
    '<span>Many</span><history mode="push" url="one"></history><history mode="replace" url="two"></history>'
  ];
  await installFakeServer(page, { actionResponses: invalidBodies.map(body => ({ body })) });

  const state = await page.evaluate(async count => {
    document.body.innerHTML = '<div id="target">Old</div>';
    const errors = [];
    document.addEventListener("heimdall:history-error", event => errors.push(event.detail.error.message));
    const results = [];
    for (let index = 0; index < count; index++) {
      const result = await window.Heimdall.invoke(`History.Invalid.${index}`, {}, { target: "#target" });
      results.push({
        applied: result.history.applied,
        message: result.history.error && result.history.error.message
      });
    }
    return {
      path: location.pathname,
      errors,
      results,
      directiveCount: document.querySelectorAll("history").length
    };
  }, invalidBodies.length);

  assert.equal(state.path, "/");
  assert.equal(state.directiveCount, 0);
  assert.equal(state.results.every(result => result.applied === false), true);
  assert.equal(state.errors.length, invalidBodies.length);
  assert.match(state.errors[0], /same-origin/);
  assert.match(state.errors[1], /Protocol-relative/);
  assert.match(state.errors[2], /backslashes/);
  assert.match(state.errors[3], /mode/);
  assert.match(state.errors[4], /required/);
  assert.match(state.errors[5], /only one/);
}

async function testRedirectAndErrorsSuppressHistory(page) {
  await installFakeServer(page, {
    actionResponses: [
      { body: '<history mode="push" url="should-not-win"></history><redirect url="#redirected"></redirect><span>Ignored</span>' },
      { status: 500, body: '<history mode="push" url="server-error"></history><span>Failure</span>' }
    ]
  });

  const state = await page.evaluate(async () => {
    const historyEvents = [];
    document.addEventListener("heimdall:history-after", event => historyEvents.push(event.detail.url));
    const redirected = await window.Heimdall.invoke("History.Redirect", {}, { swap: "none" });
    const failed = await window.Heimdall.invoke("History.Error", {}, { swap: "none" });
    return {
      hash: location.hash,
      redirectedHistory: redirected.history || null,
      failedHistory: failed.history || null,
      failedError: failed.error,
      historyEvents
    };
  });

  assert.equal(state.hash, "#redirected");
  assert.equal(state.redirectedHistory, null);
  assert.equal(state.failedHistory, null);
  assert.equal(state.failedError.includes("<history"), false);
  assert.deepEqual(state.historyEvents, []);
}

async function testAbortStillAllowsExplicitHistory(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<abort reason="oob-only"></abort><history mode="push" url="orders/saved"></history>' }]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target">Keep</div>';
    const result = await window.Heimdall.invoke("History.Abort", {}, { target: "#target" });
    return {
      path: location.pathname,
      target: document.querySelector("#target").textContent,
      abortSwap: result.abortSwap,
      historyApplied: result.history.applied
    };
  });

  assert.deepEqual(state, {
    path: "/orders/saved",
    target: "Keep",
    abortSwap: true,
    historyApplied: true
  });
}

async function testManagedPopstateCanBeIntercepted(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<history mode="push" url="orders/42"></history>' }]
  });

  const state = await page.evaluate(async () => {
    await window.Heimdall.invoke("History.Push", {}, { swap: "none" });
    const popped = new Promise(resolve => {
      document.addEventListener("heimdall:history-pop", event => {
        event.preventDefault();
        resolve({ url: event.detail.url, cancelable: event.cancelable });
      }, { once: true });
    });
    history.back();
    return { event: await popped, path: location.pathname };
  });

  assert.deepEqual(state, {
    event: { url: "/", cancelable: true },
    path: "/"
  });
}

export const tests = [
  ["pushes normalized root-relative history after the DOM swap", testPushNormalizesRootRelativeUrls],
  ["treats leading-slash and rootless history URLs identically", testLeadingSlashAndRootlessUrlsAreEquivalent],
  ["replaces history with query, hash, and same-origin absolute URLs", testReplaceAndSameOriginUrlForms],
  ["lets history lifecycle handlers modify or cancel updates", testHistoryLifecycleCanModifyOrCancel],
  ["strips and reports invalid history directives", testInvalidHistoryIsStrippedAndReported],
  ["lets redirects and error responses suppress history", testRedirectAndErrorsSuppressHistory],
  ["applies explicit history when an abort suppresses only the main swap", testAbortStillAllowsExplicitHistory],
  ["strips history directives from SSE without changing navigation", testSseStripsHistoryWithoutChangingUrl],
  ["exposes a cancellable managed popstate lifecycle event", testManagedPopstateCanBeIntercepted]
];
