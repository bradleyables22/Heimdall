import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import {
  assertLocalTimeMatrix,
  appProject,
  expectedLocalTimeFormats,
  openHarness,
  repoRoot,
  run,
  waitForActionAfter,
  waitForText,
  withStaticFileServer
} from "../helpers/e2e-support.mjs";

async function testRequestAndSwapLifecycle(page, baseUrl) {
  await openHarness(page, baseUrl, "/e2e");

  const state = await page.evaluate(async () => {
    const actionId = "e2e.lifecycle";
    const order = [];
    const snapshots = { swaps: [] };

    document.addEventListener("heimdall:request-config", event => {
      if (event.detail?.actionId !== actionId)
        return;

      order.push("request-config");
      event.detail.payload = { message: "configured" };
      event.detail.headers["X-Heimdall-E2E"] = "configured";
      event.detail.target = "#e2e-lifecycle-primary";
    });
    document.addEventListener("heimdall:request-before", event => {
      if (event.detail?.actionId !== actionId)
        return;

      order.push("request-before");
      snapshots.attempt = event.detail.attempt;
      snapshots.payload = JSON.parse(event.detail.request.body);
      snapshots.header = event.detail.request.headers["X-Heimdall-E2E"];
    });
    document.addEventListener("heimdall:swap-before", event => {
      if (event.detail?.requestContext?.actionId !== actionId)
        return;

      order.push(`swap-before:${event.detail.origin}:${event.detail.kind}`);
      snapshots.swaps.push(`before:${event.detail.kind}`);

      if (event.detail.kind === "main") {
        event.detail.target = "#e2e-lifecycle-secondary";

        const mutation = document.createElement("em");
        mutation.id = "e2e-lifecycle-listener-mutation";
        mutation.textContent = "Listener mutation";
        event.detail.fragment.append(mutation);

        const script = document.createElement("script");
        script.textContent = "window.__e2eLifecycleUnsafeScriptRan = true";
        event.detail.fragment.append(script);
      }
    });
    document.addEventListener("heimdall:swap-after", event => {
      if (event.detail?.requestContext?.actionId !== actionId)
        return;

      order.push(`swap-after:${event.detail.origin}:${event.detail.kind}`);
      snapshots.swaps.push(`after:${event.detail.kind}`);
      snapshots[`${event.detail.kind}RootId`] = event.detail.appliedRoot?.id || null;
    });
    document.addEventListener("heimdall:request-after", event => {
      if (event.detail?.actionId !== actionId)
        return;

      order.push("request-after");
      snapshots.resultOk = event.detail.result?.ok;
      snapshots.status = event.detail.response?.status;
    });
    document.addEventListener("heimdall:request-finally", event => {
      if (event.detail?.actionId !== actionId)
        return;

      order.push("request-finally");
      snapshots.finished = event.detail.finishedAt != null;
    });

    const result = await window.Heimdall.invoke(
      actionId,
      { message: "original" },
      { target: "#e2e-lifecycle-primary" });

    return {
      result: { ok: result.ok, status: result.status },
      order,
      snapshots,
      primaryText: document.querySelector("#e2e-lifecycle-primary").textContent.trim(),
      secondaryText: document.querySelector("#e2e-lifecycle-secondary").textContent.trim(),
      sideText: document.querySelector("#e2e-lifecycle-side").textContent.trim(),
      mutationCount: document.querySelectorAll("#e2e-lifecycle-listener-mutation").length,
      scriptCount: document.querySelectorAll("#e2e-lifecycle-secondary script").length,
      unsafeScriptRan: window.__e2eLifecycleUnsafeScriptRan === true
    };
  });

  assert.deepEqual(state.result, { ok: true, status: 200 });
  assert.deepEqual(state.order, [
    "request-config",
    "request-before",
    "swap-before:action:invocation",
    "swap-after:action:invocation",
    "swap-before:action:main",
    "swap-after:action:main",
    "request-after",
    "request-finally"
  ]);
  assert.deepEqual(state.snapshots, {
    swaps: ["before:invocation", "after:invocation", "before:main", "after:main"],
    attempt: 1,
    payload: { message: "configured" },
    header: "configured",
    invocationRootId: "e2e-lifecycle-side-result",
    mainRootId: "e2e-lifecycle-main-result",
    resultOk: true,
    status: 200,
    finished: true
  });
  assert.equal(state.primaryText, "Lifecycle primary original");
  assert.equal(state.secondaryText, "Lifecycle: configured | header: configuredListener mutation");
  assert.equal(state.sideText, "Lifecycle side: configured");
  assert.equal(state.mutationCount, 1);
  assert.equal(state.scriptCount, 0);
  assert.equal(state.unsafeScriptRan, false);
}

async function testLifecycleCancellation(page, baseUrl) {
  const actionRequests = [];
  page.on("request", request => {
    const actionId = new URL(request.url()).searchParams.get("action");
    if (actionId?.startsWith("e2e.lifecycle") || actionId === "e2e.sync")
      actionRequests.push(actionId);
  });

  await openHarness(page, baseUrl, "/e2e");

  const state = await page.evaluate(async () => {
    const requestCancelAction = "e2e.lifecycle.request-cancel";
    const swapCancelAction = "e2e.lifecycle.swap-cancel";
    const cancellations = [];
    const timeouts = [];
    const finallyEvents = [];
    const swapEvents = [];
    let resolveExternalStarted;
    const externalStarted = new Promise(resolve => { resolveExternalStarted = resolve; });

    document.addEventListener("heimdall:request-before", event => {
      if (event.detail?.actionId === requestCancelAction)
        event.preventDefault();
      if (event.detail?.actionId === "e2e.sync" && event.detail.payload?.label === "external")
        resolveExternalStarted();
    });
    document.addEventListener("heimdall:request-cancel", event => {
      const actionId = event.detail?.actionId;
      if (actionId === requestCancelAction || actionId === "e2e.sync") {
        cancellations.push({
          actionId,
          label: event.detail.payload?.label || null,
          reason: event.detail.result?.cancelReason
        });
      }
    });
    document.addEventListener("heimdall:request-timeout", event => {
      timeouts.push(event.detail?.payload?.label || null);
    });
    document.addEventListener("heimdall:request-finally", event => {
      const actionId = event.detail?.actionId;
      if (actionId === requestCancelAction || actionId === swapCancelAction || actionId === "e2e.sync") {
        finallyEvents.push({
          actionId,
          label: event.detail.payload?.label || null
        });
      }
    });
    document.addEventListener("heimdall:swap-before", event => {
      if (event.detail?.requestContext?.actionId !== swapCancelAction)
        return;

      swapEvents.push("before");
      event.preventDefault();
    });
    document.addEventListener("heimdall:swap-after", event => {
      if (event.detail?.requestContext?.actionId === swapCancelAction)
        swapEvents.push("after");
    });

    const requestCancelled = await window.Heimdall.invoke(
      requestCancelAction,
      {},
      { target: "#e2e-lifecycle-request-cancel-target" });

    const swapCancelled = await window.Heimdall.invoke(
      swapCancelAction,
      {},
      { target: "#e2e-lifecycle-swap-cancel-target" });

    const timedOut = await window.Heimdall.invoke(
      "e2e.sync",
      { label: "timeout", delayMs: 500 },
      { target: "#e2e-timeout-target", timeoutMs: 40 });

    const controller = new AbortController();
    const externalInvocation = window.Heimdall.invoke(
      "e2e.sync",
      { label: "external", delayMs: 500 },
      { target: "#e2e-external-abort-target", signal: controller.signal });
    await externalStarted;
    controller.abort();
    const externallyCancelled = await externalInvocation;

    return {
      requestCancelled: {
        cancelled: requestCancelled.cancelled,
        reason: requestCancelled.cancelReason
      },
      swapCancelled: { ok: swapCancelled.ok, status: swapCancelled.status },
      timedOut: { cancelled: timedOut.cancelled, reason: timedOut.cancelReason },
      externallyCancelled: {
        cancelled: externallyCancelled.cancelled,
        reason: externallyCancelled.cancelReason
      },
      cancellations,
      timeouts,
      finallyEvents,
      swapEvents,
      targets: {
        request: document.querySelector("#e2e-lifecycle-request-cancel-target").textContent.trim(),
        swap: document.querySelector("#e2e-lifecycle-swap-cancel-target").textContent.trim(),
        timeout: document.querySelector("#e2e-timeout-target").textContent.trim(),
        external: document.querySelector("#e2e-external-abort-target").textContent.trim()
      }
    };
  });

  assert.deepEqual(state.requestCancelled, { cancelled: true, reason: "event-cancelled" });
  assert.deepEqual(state.swapCancelled, { ok: true, status: 200 });
  assert.deepEqual(state.timedOut, { cancelled: true, reason: "timeout" });
  assert.deepEqual(state.externallyCancelled, { cancelled: true, reason: "external-signal" });
  assert.deepEqual(state.cancellations, [
    { actionId: "e2e.lifecycle.request-cancel", label: null, reason: "event-cancelled" },
    { actionId: "e2e.sync", label: "timeout", reason: "timeout" },
    { actionId: "e2e.sync", label: "external", reason: "external-signal" }
  ]);
  assert.deepEqual(state.timeouts, ["timeout"]);
  assert.deepEqual(state.swapEvents, ["before"]);
  assert.deepEqual(state.targets, {
    request: "Request cancel target original",
    swap: "Swap cancel target original",
    timeout: "Timeout target original",
    external: "External abort target original"
  });
  assert.equal(state.finallyEvents.length, 4);
  assert.deepEqual(actionRequests, [
    "e2e.lifecycle.swap-cancel",
    "e2e.sync",
    "e2e.sync"
  ]);
}

export const tests = [
  ["emits mutable request and swap lifecycle events through the hosted stack", testRequestAndSwapLifecycle],
  ["honors request, swap, timeout, and external cancellation through the hosted stack", testLifecycleCancellation]
];
