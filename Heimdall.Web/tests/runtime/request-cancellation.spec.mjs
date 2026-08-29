import assert from "node:assert/strict";
import {
  actionFetches,
  csrfFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";

async function testRequestLifecycleCancellation(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="should-not-run">No</span>' }]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target">Keep</div>';
    const order = [];

    document.addEventListener("heimdall:request-config", () => order.push("config"));
    document.addEventListener("heimdall:request-before", event => {
      order.push("before");
      event.preventDefault();
    });
    document.addEventListener("heimdall:request-cancel", event => {
      order.push(`cancel:${event.detail.result.cancelReason}`);
    });
    document.addEventListener("heimdall:request-finally", () => order.push("finally"));

    const result = await window.Heimdall.invoke("Lifecycle.Cancel", {}, { target: "#target" });
    return {
      result: {
        cancelled: result.cancelled,
        cancelReason: result.cancelReason
      },
      order,
      targetHtml: document.querySelector("#target").innerHTML
    };
  });

  assert.deepEqual(state.result, { cancelled: true, cancelReason: "event-cancelled" });
  assert.deepEqual(state.order, ["config", "before", "cancel:event-cancelled", "finally"]);
  assert.equal(state.targetHtml, "Keep");

  const fetches = await getFetches(page);
  assert.equal(csrfFetches(fetches).length, 1);
  assert.equal(actionFetches(fetches).length, 0);
}

async function testRequestTimeout(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="late-timeout">Late</span>', delayMs: 200 }]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target">Keep</div>';
    window.Heimdall.config.requestTimeoutMs = 20;
    const events = [];
    document.addEventListener("heimdall:request-timeout", () => events.push("timeout"));
    document.addEventListener("heimdall:request-cancel", event => events.push(`cancel:${event.detail.result.cancelReason}`));
    document.addEventListener("heimdall:request-finally", () => events.push("finally"));

    const started = performance.now();
    const result = await window.Heimdall.invoke("Timeout.Run", {}, { target: "#target" });

    return {
      result: {
        cancelled: result.cancelled,
        cancelReason: result.cancelReason
      },
      events,
      elapsed: performance.now() - started,
      targetHtml: document.querySelector("#target").innerHTML
    };
  });

  assert.deepEqual(state.result, { cancelled: true, cancelReason: "timeout" });
  assert.deepEqual(state.events, ["timeout", "cancel:timeout", "finally"]);
  assert.ok(state.elapsed < 150);
  assert.equal(state.targetHtml, "Keep");

  const action = actionFetches(await getFetches(page))[0];
  assert.equal(action.aborted, true);
}

async function testExternalAbortSignal(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="late-abort">Late</span>', delayMs: 200 }]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target">Keep</div>';
    const controller = new AbortController();
    const cancellations = [];
    document.addEventListener("heimdall:request-cancel", event => {
      cancellations.push(event.detail.result.cancelReason);
    });

    let resolveStarted;
    const started = new Promise(resolve => { resolveStarted = resolve; });
    document.addEventListener("heimdall:request-before", event => {
      if (event.detail.actionId === "Abort.External")
        resolveStarted();
    });

    const invocation = window.Heimdall.invoke("Abort.External", {}, {
      target: "#target",
      signal: controller.signal
    });
    await started;
    controller.abort();
    const result = await invocation;

    return {
      result: {
        cancelled: result.cancelled,
        cancelReason: result.cancelReason
      },
      cancellations,
      targetHtml: document.querySelector("#target").innerHTML
    };
  });

  assert.deepEqual(state.result, { cancelled: true, cancelReason: "external-signal" });
  assert.deepEqual(state.cancellations, ["external-signal"]);
  assert.equal(state.targetHtml, "Keep");
  assert.equal(actionFetches(await getFetches(page))[0].aborted, true);
}

export const tests = [
  ["cancels requests from lifecycle events", testRequestLifecycleCancellation],
  ["cancels timed out requests without applying responses", testRequestTimeout],
  ["honors external abort signals", testExternalAbortSignal]
];
