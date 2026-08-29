import assert from "node:assert/strict";
import { openHarness } from "../helpers/e2e-support.mjs";

async function testAsyncRequestHeadersReachHostedAction(page, baseUrl) {
  await openHarness(page, baseUrl, "/e2e");

  const state = await page.evaluate(async () => {
    window.__heimdallRequestHeaderContexts = [];
    window.Heimdall.config.requestHeaders = async context => {
      await new Promise(resolve => setTimeout(resolve, 5));
      window.__heimdallRequestHeaderContexts.push({
        kind: context.kind,
        actionId: context.actionId,
        requestId: context.requestId,
        attempt: context.attempt,
        hasSignal: !!context.signal
      });

      if (context.kind !== "content-action")
        return {};

      context.headers.Authorization = "Bearer hosted-jwt";
      return { "X-Heimdall-E2E": "async-provider" };
    };

    const result = await window.Heimdall.invoke("e2e.request-headers", {}, {
      target: "#e2e-error-target"
    });

    return {
      ok: result.ok,
      status: result.status,
      text: document.querySelector("#e2e-request-headers-result")?.textContent || null,
      contexts: window.__heimdallRequestHeaderContexts
    };
  });

  assert.equal(state.ok, true);
  assert.equal(state.status, 200);
  assert.equal(state.text, "Bearer hosted-jwt|async-provider");
  const actionContext = state.contexts.find(context => context.kind === "content-action");
  assert.ok(actionContext);
  assert.equal(actionContext.actionId, "e2e.request-headers");
  assert.ok(actionContext.requestId > 0);
  assert.equal(actionContext.attempt, 1);
  assert.equal(actionContext.hasSignal, true);
}

async function testHostedUnauthorizedEventCanTakeControl(page, baseUrl) {
  await openHarness(page, baseUrl, "/e2e");

  const state = await page.evaluate(async () => {
    const events = [];
    document.addEventListener("heimdall:unauthorized", event => {
      events.push({
        kind: event.detail.kind,
        actionId: event.detail.actionId,
        status: event.detail.status,
        body: event.detail.body,
        cancelable: event.cancelable
      });
      event.preventDefault();
      document.querySelector("#e2e-error-target").textContent = "Application login UI opened";
    });

    const result = await window.Heimdall.invoke("e2e.unauthorized", {}, {
      target: "#e2e-error-target"
    });

    return {
      ok: result.ok,
      status: result.status,
      events,
      target: document.querySelector("#e2e-error-target").textContent.trim()
    };
  });

  assert.equal(state.ok, false);
  assert.equal(state.status, 401);
  assert.equal(state.events.length, 1);
  assert.equal(state.events[0].kind, "content-action");
  assert.equal(state.events[0].actionId, "e2e.unauthorized");
  assert.equal(state.events[0].status, 401);
  assert.match(state.events[0].body, /Authentication required/);
  assert.equal(state.events[0].cancelable, true);
  assert.equal(state.target, "Application login UI opened");
}

export const tests = [
  ["awaits request headers before a hosted content action", testAsyncRequestHeadersReachHostedAction],
  [
    "lets hosted unauthorized handlers take control",
    testHostedUnauthorizedEventCanTakeControl,
    { allowedBrowserErrors: [/Failed to load resource: the server responded with a status of 401/] }
  ]
];
