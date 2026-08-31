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

async function testDynamicBootAndSwapModes(page, baseUrl) {
  await openHarness(page, baseUrl, "/e2e");

  await page.locator("#e2e-dynamic-button").click();
  await waitForText(page.locator("#e2e-dynamic-load-target"), "Dynamic load completed");

  await page.locator("#e2e-outer-button").click();
  await waitForText(page.locator("#e2e-outer-target"), "Outer swapped");
  assert.equal(await page.locator("#e2e-outer-result").count(), 1);

  await page.locator("#e2e-beforeend-button").click();
  await page.locator("#e2e-beforeend-result").waitFor();
  assert.deepEqual(await page.locator("#e2e-beforeend-target > span").evaluateAll(nodes => nodes.map(node => node.id)), [
    "e2e-beforeend-original",
    "e2e-beforeend-result"
  ]);

  await page.locator("#e2e-afterbegin-button").click();
  await page.locator("#e2e-afterbegin-result").waitFor();
  assert.deepEqual(await page.locator("#e2e-afterbegin-target > span").evaluateAll(nodes => nodes.map(node => node.id)), [
    "e2e-afterbegin-result",
    "e2e-afterbegin-original"
  ]);

  const noneCompleted = waitForActionAfter(page, "e2e.swap.none");
  await page.locator("#e2e-none-button").click();
  await noneCompleted;
  await waitForText(page.locator("#e2e-none-target"), "None target original");
  assert.equal(await page.locator("#e2e-none-result").count(), 0);
}

async function testResponseDirectives(page, baseUrl) {
  await page.addInitScript(() => {
    window.__heimdallE2EAbortReasons = [];
    document.addEventListener("heimdall:abort", event => {
      window.__heimdallE2EAbortReasons.push(event.detail && event.detail.reason);
    });
  });

  await openHarness(page, baseUrl, "/e2e");

  await page.locator("#e2e-swap-button").click();
  await waitForText(page.locator("#e2e-swap-target"), "Swap completed");

  await page.locator("#e2e-oob-button").click();
  await waitForText(page.locator("#e2e-oob-main-target"), "OOB main completed");
  await waitForText(page.locator("#e2e-oob-side-target"), "OOB side completed");

  await page.locator("#e2e-abort-button").click();
  await waitForText(page.locator("#e2e-abort-target"), "Abort target original");
  await waitForText(page.locator("#e2e-abort-side"), "Abort side completed");
  await page.waitForFunction(() => (window.__heimdallE2EAbortReasons || []).includes("e2e-abort"));

  await page.locator("#e2e-redirect-button").click();
  await page.waitForFunction(() => window.location.hash === "#e2e-redirected");
  await waitForText(page.locator("#e2e-redirect-target"), "Redirect target original");
}

async function testJsInvokeVoid(page, baseUrl) {
  await openHarness(page, baseUrl, "/e2e");

  await page.locator("#e2e-js-button").click();
  await waitForText(page.locator("#e2e-js-target"), "JS target swapped");
  await waitForText(page.locator("#e2e-js-log"), "before:ok:JS target original|after:ok:JS target swapped");

  assert.deepEqual(await page.evaluate(() => window.HeimdallE2E.calls), [
    { phase: "before", value: "ok", targetText: "JS target original" },
    { phase: "after", value: "ok", targetText: "JS target swapped" }
  ]);
}

export const tests = [
  ["boots dynamically swapped content and applies swap modes", testDynamicBootAndSwapModes],
  ["applies swaps, OOB, abort, and redirect directives", testResponseDirectives],
  ["invokes JavaScript before and after swaps", testJsInvokeVoid]
];
