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

async function testHarnessSse(page, baseUrl) {
  await page.addInitScript(() => {
    window.__heimdallE2ESseOpen = false;
    window.__heimdallE2ESseSwaps = [];
    document.addEventListener("heimdall:sse-open", event => {
      if (event.detail && event.detail.topic === "e2e-harness")
        window.__heimdallE2ESseOpen = true;
    });
    document.addEventListener("heimdall:swap-before", event => {
      if (event.detail?.origin === "sse" && event.detail?.sourceElement?.id === "e2e-sse-host")
        window.__heimdallE2ESseSwaps.push(`before:${event.detail.kind}`);
    });
    document.addEventListener("heimdall:swap-after", event => {
      if (event.detail?.origin === "sse" && event.detail?.sourceElement?.id === "e2e-sse-host")
        window.__heimdallE2ESseSwaps.push(`after:${event.detail.kind}`);
    });
  });

  await openHarness(page, baseUrl, "/e2e");
  await page.waitForFunction(() => window.__heimdallE2ESseOpen === true, null, { timeout: 8000 });

  await page.locator("#e2e-sse-button").click();
  await waitForText(page.locator("#e2e-sse-target"), "SSE delivered", 10000);

  await page.locator("#e2e-sse-oob-button").click();
  await waitForText(page.locator("#e2e-sse-target"), "SSE OOB main", 10000);
  await waitForText(page.locator("#e2e-sse-oob-target"), "SSE OOB side", 10000);

  await page.locator("#e2e-sse-js-button").click();
  await waitForText(page.locator("#e2e-sse-target"), "SSE JS main", 10000);
  await waitForText(page.locator("#e2e-sse-js-log"), "sse-ok:SSE JS main:1", 10000);
  assert.deepEqual(await page.evaluate(() => window.HeimdallE2E.sseCalls), [
    { value: "sse-ok", targetText: "SSE JS main" }
  ]);
  assert.deepEqual(await page.evaluate(() => window.__heimdallE2ESseSwaps), [
    "before:main",
    "after:main",
    "before:invocation",
    "after:invocation",
    "before:main",
    "after:main",
    "before:main",
    "after:main"
  ]);
}

export const tests = [
  ["receives deterministic Bifrost SSE updates, OOB, and JS directives", testHarnessSse]
];
