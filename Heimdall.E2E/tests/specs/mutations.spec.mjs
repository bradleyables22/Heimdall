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

async function testHostedMutations(page, baseUrl) {
  await page.addInitScript(() => {
    window.__heimdallMutationEvents = [];
    window.__heimdallMutationSseOpen = false;
    document.addEventListener("heimdall:sse-open", event => {
      if (event.detail?.topic === "e2e-harness")
        window.__heimdallMutationSseOpen = true;
    });
    document.addEventListener("heimdall:mutation-before", event => {
      window.__heimdallMutationEvents.push({
        phase: "before",
        origin: event.detail?.origin,
        target: event.detail?.targetSelector,
        source: event.detail?.sourceElement?.id || null
      });
    });
    document.addEventListener("heimdall:mutation-after", event => {
      window.__heimdallMutationEvents.push({
        phase: "after",
        origin: event.detail?.origin,
        target: event.detail?.targetSelector,
        source: event.detail?.sourceElement?.id || null,
        targets: event.detail?.targetCount,
        operations: event.detail?.operationCount
      });
    });
  });

  await openHarness(page, baseUrl, "/e2e");
  await page.waitForFunction(() => window.__heimdallMutationSseOpen === true, null, { timeout: 8000 });

  const actionResult = await page.evaluate(async () => {
    const panel = document.querySelector("#e2e-mutation-panel");
    const input = document.querySelector("#e2e-mutation-input");
    const source = document.querySelector("#e2e-mutation-button");
    panel.__identityMarker = "panel-preserved";
    input.__identityMarker = "input-preserved";
    input.value = "client edited";
    let inputEvents = 0;
    input.addEventListener("input", () => inputEvents++);
    input.focus();

    const result = await window.Heimdall.invoke("e2e.mutation", {}, {
      target: "#e2e-mutation-main-target",
      sourceEl: source
    });
    input.dispatchEvent(new Event("input", { bubbles: true }));

    return {
      ok: result.ok,
      panelSame: panel === document.querySelector("#e2e-mutation-panel"),
      inputSame: input === document.querySelector("#e2e-mutation-input"),
      panelMarker: panel.__identityMarker,
      inputMarker: input.__identityMarker,
      inputValue: input.value,
      inputEvents,
      focused: document.activeElement === input
    };
  });

  assert.deepEqual(actionResult, {
    ok: true,
    panelSame: true,
    inputSame: true,
    panelMarker: "panel-preserved",
    inputMarker: "input-preserved",
    inputValue: "client edited",
    inputEvents: 1,
    focused: true
  });
  await waitForText(page.locator("#e2e-mutation-main-target"), "Mutation main swapped");
  await waitForText(page.locator("#e2e-mutation-order-host"), "Created before mutation");

  const actionDom = await page.evaluate(() => {
    const panel = document.querySelector("#e2e-mutation-panel");
    const ordered = document.querySelector("#e2e-mutation-order-created");
    return {
      panelClass: panel.className,
      server: panel.getAttribute("data-server"),
      removed: panel.hasAttribute("data-remove"),
      state: JSON.parse(panel.getAttribute("data-heimdall-state")),
      childValues: Array.from(panel.querySelectorAll(".e2e-mutation-child"))
        .map(element => element.getAttribute("data-child")),
      orderClass: ordered.className,
      orderValue: ordered.getAttribute("data-command-order"),
      directives: document.querySelectorAll("mutation, mutation-attr, mutation-class").length
    };
  });
  assert.deepEqual(actionDom, {
    panelClass: "keep ready",
    server: "action",
    removed: false,
    state: { Count: 41 },
    childValues: ["action", "action"],
    orderClass: "ordered",
    orderValue: "invocation-then-mutation",
    directives: 0
  });

  await page.locator("#e2e-mutation-state-button").click();
  await waitForText(page.locator("#e2e-mutation-state-result"), "Mutation state count: 41");

  await page.locator("#e2e-mutation-sse-button").click();
  await waitForText(page.locator("#e2e-sse-target"), "Mutation SSE main swapped", 10000);
  await page.waitForFunction(() =>
    document.querySelector("#e2e-mutation-panel")?.getAttribute("data-server") === "sse"
  );

  const finalState = await page.evaluate(() => ({
    panelClass: document.querySelector("#e2e-mutation-panel").className,
    server: document.querySelector("#e2e-mutation-panel").getAttribute("data-server"),
    state: JSON.parse(document.querySelector("#e2e-mutation-panel").getAttribute("data-heimdall-state")),
    inputValue: document.querySelector("#e2e-mutation-input").value,
    events: window.__heimdallMutationEvents
  }));

  assert.equal(finalState.panelClass, "keep ready sse-ready");
  assert.equal(finalState.server, "sse");
  assert.deepEqual(finalState.state, { Count: 41 });
  assert.equal(finalState.inputValue, "client edited");
  assert.deepEqual(finalState.events, [
    {
      phase: "before", origin: "action", target: "#e2e-mutation-order-created",
      source: "e2e-mutation-button"
    },
    {
      phase: "after", origin: "action", target: "#e2e-mutation-order-created",
      source: "e2e-mutation-button", targets: 1, operations: 2
    },
    {
      phase: "before", origin: "action", target: "#e2e-mutation-panel",
      source: "e2e-mutation-button"
    },
    {
      phase: "after", origin: "action", target: "#e2e-mutation-panel",
      source: "e2e-mutation-button", targets: 1, operations: 5
    },
    {
      phase: "before", origin: "action", target: "#e2e-mutation-panel",
      source: "e2e-mutation-button"
    },
    {
      phase: "after", origin: "action", target: "#e2e-mutation-panel",
      source: "e2e-mutation-button", targets: 2, operations: 1
    },
    {
      phase: "before", origin: "sse", target: "#e2e-mutation-panel",
      source: "e2e-sse-host"
    },
    {
      phase: "after", origin: "sse", target: "#e2e-mutation-panel",
      source: "e2e-sse-host", targets: 1, operations: 2
    }
  ]);
}

export const tests = [
  ["mutates live DOM and state through hosted actions and Bifrost SSE", testHostedMutations]
];
