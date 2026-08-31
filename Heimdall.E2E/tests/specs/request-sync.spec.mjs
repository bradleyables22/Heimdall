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

async function testRequestSynchronization(page, baseUrl) {
  const actionPayloads = [];
  page.on("request", request => {
    const url = new URL(request.url());
    if (url.searchParams.get("action") !== "e2e.sync")
      return;

    try {
      actionPayloads.push(request.postDataJSON());
    } catch {
      actionPayloads.push(null);
    }
  });

  await openHarness(page, baseUrl, "/e2e");

  const defaults = await page.evaluate(() => ({
    requestSync: window.Heimdall.config.requestSync,
    requestTimeoutMs: window.Heimdall.config.requestTimeoutMs
  }));
  assert.deepEqual(defaults, { requestSync: "parallel", requestTimeoutMs: 0 });

  const renderedAttributes = await page.evaluate(() => {
    const read = id => {
      const element = document.querySelector(id);
      return {
        sync: element.getAttribute("heimdall-sync"),
        group: element.getAttribute("heimdall-sync-group"),
        disable: element.getAttribute("heimdall-content-disable")
      };
    };

    return {
      parallel: read("#e2e-sync-parallel-slow"),
      replace: read("#e2e-sync-replace-slow"),
      drop: read("#e2e-sync-drop-slow"),
      queue: read("#e2e-sync-queue-first"),
      existingControlSync: document.querySelector("#e2e-swap-button").getAttribute("heimdall-sync")
    };
  });

  assert.deepEqual(renderedAttributes, {
    parallel: { sync: null, group: null, disable: "false" },
    replace: { sync: "replace", group: "e2e-sync-replace", disable: "false" },
    drop: { sync: "drop", group: "e2e-sync-drop", disable: "false" },
    queue: { sync: "queue-latest", group: "e2e-sync-queue", disable: "false" },
    existingControlSync: null
  });

  const state = await page.evaluate(async () => {
    const before = [];
    const completed = [];
    const cancellations = [];
    const labelOf = payload => payload?.label ?? payload?.Label;

    document.addEventListener("heimdall:request-before", event => {
      if (event.detail?.actionId === "e2e.sync")
        before.push(labelOf(event.detail.payload));
    });
    document.addEventListener("heimdall:request-finally", event => {
      if (event.detail?.actionId === "e2e.sync")
        completed.push(labelOf(event.detail.payload));
    });
    document.addEventListener("heimdall:request-cancel", event => {
      if (event.detail?.actionId === "e2e.sync") {
        cancellations.push({
          label: labelOf(event.detail.payload),
          reason: event.detail.result?.cancelReason
        });
      }
    });

    async function waitUntil(predicate, description) {
      const started = performance.now();
      while (!predicate()) {
        if (performance.now() - started > 5000)
          throw new Error(`Timed out waiting for ${description}.`);
        await new Promise(resolve => setTimeout(resolve, 10));
      }
    }

    const click = id => document.querySelector(id).click();

    click("#e2e-sync-parallel-slow");
    await waitUntil(() => before.includes("parallel-slow"), "parallel slow request");
    click("#e2e-sync-parallel-fast");
    await waitUntil(
      () => completed.includes("parallel-slow") && completed.includes("parallel-fast"),
      "parallel requests");
    const parallelText = document.querySelector("#e2e-sync-parallel-target").textContent.trim();

    click("#e2e-sync-replace-slow");
    await waitUntil(() => before.includes("replace-slow"), "replace slow request");
    click("#e2e-sync-replace-fast");
    await waitUntil(
      () => completed.includes("replace-slow") && completed.includes("replace-fast"),
      "replace requests");
    const replaceText = document.querySelector("#e2e-sync-replace-target").textContent.trim();

    click("#e2e-sync-drop-slow");
    await waitUntil(() => before.includes("drop-slow"), "drop slow request");
    click("#e2e-sync-drop-fast");
    await waitUntil(
      () => completed.includes("drop-slow") && completed.includes("drop-fast"),
      "drop requests");
    const dropText = document.querySelector("#e2e-sync-drop-target").textContent.trim();

    click("#e2e-sync-queue-first");
    await waitUntil(() => before.includes("queue-first"), "first queued request");
    click("#e2e-sync-queue-second");
    click("#e2e-sync-queue-third");
    await waitUntil(
      () => completed.includes("queue-first") &&
        completed.includes("queue-second") &&
        completed.includes("queue-third"),
      "queued requests");
    const queueText = document.querySelector("#e2e-sync-queue-target").textContent.trim();

    return {
      before,
      completed,
      cancellations,
      targets: { parallelText, replaceText, dropText, queueText }
    };
  });

  assert.deepEqual(state.targets, {
    parallelText: "Sync: parallel-slow",
    replaceText: "Sync: replace-fast",
    dropText: "Sync: drop-slow",
    queueText: "Sync: queue-third"
  });
  assert.deepEqual(state.cancellations, [
    { label: "replace-slow", reason: "replaced" },
    { label: "drop-fast", reason: "dropped" },
    { label: "queue-second", reason: "queue-replaced" }
  ]);
  assert.deepEqual(state.before, [
    "parallel-slow",
    "parallel-fast",
    "replace-slow",
    "replace-fast",
    "drop-slow",
    "queue-first",
    "queue-third"
  ]);
  assert.equal(state.completed.length, 9);
  assert.deepEqual(actionPayloads.map(payload => payload?.label ?? payload?.Label), state.before);
}

async function testQueuedStateAndTargetRefresh(page, baseUrl) {
  const requests = [];
  page.on("request", request => {
    const actionId = new URL(request.url()).searchParams.get("action");
    if (actionId !== "e2e.sync.state" &&
        actionId !== "e2e.sync.outer-target" &&
        actionId !== "e2e.sync")
      return;

    let payload = null;
    try {
      payload = request.postDataJSON();
    } catch {
      // Keep null for requests without a JSON payload.
    }
    requests.push({ actionId, payload });
  });

  await openHarness(page, baseUrl, "/e2e");

  const state = await page.evaluate(async () => {
    const before = [];
    const finallyEvents = [];
    document.addEventListener("heimdall:request-before", event => {
      if (event.detail?.actionId === "e2e.sync.state" ||
          event.detail?.actionId === "e2e.sync.outer-target" ||
          event.detail?.actionId === "e2e.sync") {
        before.push({
          actionId: event.detail.actionId,
          count: event.detail.payload?.count ?? event.detail.payload?.Count ?? null,
          label: event.detail.payload?.label ?? event.detail.payload?.Label ?? null
        });
      }
    });
    document.addEventListener("heimdall:request-finally", event => {
      if (event.detail?.actionId === "e2e.sync.state")
        finallyEvents.push(event.detail.actionId);
    });

    async function waitUntil(predicate, description) {
      const started = performance.now();
      while (!predicate()) {
        if (performance.now() - started > 5000)
          throw new Error(`Timed out waiting for ${description}.`);
        await new Promise(resolve => setTimeout(resolve, 10));
      }
    }

    const stateButton = document.querySelector("#e2e-sync-live-state-button");
    stateButton.click();
    await waitUntil(
      () => before.filter(entry => entry.actionId === "e2e.sync.state").length === 1,
      "first live-state request");
    stateButton.click();
    await waitUntil(() => finallyEvents.length === 2, "both live-state requests");

    const liveState = JSON.parse(
      document.querySelector("#e2e-sync-live-state").getAttribute("data-heimdall-state"));
    const liveStateText = document.querySelector("#e2e-sync-live-state-result").textContent.trim();

    const originalTarget = document.querySelector("#e2e-sync-replaced-target");
    const firstTargetRequest = window.Heimdall.invoke("e2e.sync.outer-target", {}, {
      target: "#e2e-sync-replaced-target",
      swap: "outer",
      sync: "queue-latest",
      syncGroup: "e2e-sync-replaced-target"
    });
    await waitUntil(
      () => before.some(entry => entry.actionId === "e2e.sync.outer-target"),
      "outer target request");
    const secondTargetRequest = window.Heimdall.invoke("e2e.sync", {
      label: "target-second",
      delayMs: 20
    }, {
      target: "#e2e-sync-replaced-target",
      swap: "inner",
      sync: "queue-latest",
      syncGroup: "e2e-sync-replaced-target"
    });
    const [firstTargetResult, secondTargetResult] = await Promise.all([
      firstTargetRequest,
      secondTargetRequest
    ]);

    const currentTarget = document.querySelector("#e2e-sync-replaced-target");
    return {
      before,
      liveState,
      liveStateText,
      targets: {
        firstOk: firstTargetResult.ok,
        secondOk: secondTargetResult.ok,
        originalConnected: originalTarget.isConnected,
        currentIsOriginal: currentTarget === originalTarget,
        currentText: currentTarget.textContent.trim()
      }
    };
  });

  const stateBefore = state.before.filter(entry => entry.actionId === "e2e.sync.state");
  assert.deepEqual(stateBefore.map(entry => entry.count), [0, 1]);
  assert.equal(state.liveState.Count ?? state.liveState.count, 2);
  assert.equal(state.liveStateText, "Queued state: 2");
  assert.deepEqual(state.targets, {
    firstOk: true,
    secondOk: true,
    originalConnected: false,
    currentIsOriginal: false,
    currentText: "Sync: target-second"
  });

  const stateRequests = requests.filter(request => request.actionId === "e2e.sync.state");
  assert.deepEqual(
    stateRequests.map(request => request.payload?.count ?? request.payload?.Count),
    [0, 1]);
  assert.equal(
    requests.filter(request => request.actionId === "e2e.sync.outer-target").length,
    1);
  assert.equal(
    requests.filter(request =>
      request.actionId === "e2e.sync" &&
      (request.payload?.label ?? request.payload?.Label) === "target-second").length,
    1);
}

export const tests = [
  ["coordinates parallel, replace, drop, and queue-latest requests through the hosted stack", testRequestSynchronization],
  ["refreshes queued state and replaced targets through the hosted stack", testQueuedStateAndTargetRefresh]
];
