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

async function testHarnessBootAndLoad(page, baseUrl) {
  await openHarness(page, baseUrl, "/");
  await waitForText(page.locator("#e2e-load-target"), "Load completed");

  const runtime = await page.evaluate(() => ({
    hasHeimdall: !!window.Heimdall,
    scriptCount: document.scripts.length,
    aliasHref: location.pathname
  }));

  assert.equal(runtime.hasHeimdall, true);
  assert.ok(runtime.scriptCount > 0);
  assert.equal(runtime.aliasHref, "/");

  await openHarness(page, baseUrl, "/e2e");
  await waitForText(page.locator("#e2e-load-target"), "Load completed");
}

async function testFluentLocalTime(page, baseUrl) {
  await page.addInitScript(() => {
    window.__heimdallLocalTimeEvents = [];
    window.__heimdallLocalTimeSseOpen = false;

    document.addEventListener("heimdall:sse-open", event => {
      if (event.detail?.topic === "e2e-local-time")
        window.__heimdallLocalTimeSseOpen = true;
    });

    document.addEventListener("heimdall:time-after", event => {
      const element = event.detail?.element;
      if (!element?.id?.startsWith("e2e-local-time-"))
        return;

      window.__heimdallLocalTimeEvents.push({
        id: element.id,
        connected: element.isConnected,
        locale: event.detail.locale,
        origin: event.detail.origin,
        kind: event.detail.kind
      });
    });
  });

  const requestedUrls = [];
  page.on("request", request => requestedUrls.push(request.url()));

  await openHarness(page, baseUrl);

  await assertLocalTimeMatrix(page, "e2e-local-time-initial");
  const initialComposite = page.locator("#e2e-local-time-initial-composite");
  assert.equal(await initialComposite.getAttribute("heimdall-time"), "2026-08-06T08:05:07.123Z");
  assert.equal(
    await initialComposite.getAttribute("heimdall-time-format"),
    "dddd, MMMM d, yyyy 'at' h:mm:ss.fff tt zzz"
  );

  await waitForText(
    page.locator("#e2e-local-time-load-result"),
    "2026-08-06 04:05:07.123 -04:00"
  );

  await page.locator("#e2e-local-time-action-button").click();
  await assertLocalTimeMatrix(page, "e2e-local-time-action");

  await page.locator("#e2e-local-time-oob-button").click();
  await waitForText(page.locator("#e2e-local-time-oob-main-result"), "August 6 04:05");
  await waitForText(page.locator("#e2e-local-time-oob-side-result"), "août 6 04:05");

  await page.waitForFunction(() => window.__heimdallLocalTimeSseOpen === true);
  await page.locator("#e2e-local-time-sse-button").click();
  await waitForText(
    page.locator("#e2e-local-time-sse-target #e2e-local-time-sse-result"),
    "2026-08-06 04:05:07.123 -04:00",
    10000
  );

  const lifecycle = await page.evaluate(() => window.__heimdallLocalTimeEvents);
  const eventFor = (id, predicate = () => true) =>
    lifecycle.find(event => event.id === id && predicate(event));

  assert.deepEqual(eventFor("e2e-local-time-initial-composite"), {
    id: "e2e-local-time-initial-composite",
    connected: true,
    locale: "en",
    origin: "boot",
    kind: null
  });
  assert.deepEqual(eventFor("e2e-local-time-load-result"), {
    id: "e2e-local-time-load-result",
    connected: false,
    locale: "en",
    origin: "action",
    kind: "main"
  });
  assert.deepEqual(eventFor("e2e-local-time-action-composite"), {
    id: "e2e-local-time-action-composite",
    connected: false,
    locale: "en",
    origin: "action",
    kind: "main"
  });
  assert.deepEqual(eventFor("e2e-local-time-oob-side-result"), {
    id: "e2e-local-time-oob-side-result",
    connected: false,
    locale: "fr-FR",
    origin: "action",
    kind: "invocation"
  });
  assert.deepEqual(eventFor("e2e-local-time-oob-main-result"), {
    id: "e2e-local-time-oob-main-result",
    connected: false,
    locale: "en",
    origin: "action",
    kind: "main"
  });
  assert.ok(eventFor(
    "e2e-local-time-sse-result",
    event => event.connected === false && event.origin === "sse" && event.kind === "main"
  ));
  assert.equal(
    requestedUrls.some(url => new URL(url).pathname.toLowerCase().includes("/time")),
    false,
    "Local time formatting should not call a Heimdall server endpoint."
  );
}

export const tests = [
  ["boots the harness and runs load triggers", testHarnessBootAndLoad],
  [
    "localizes times across initial, load, action, OOB, and SSE delivery paths",
    testFluentLocalTime,
    { contextOptions: { locale: "en-US", timezoneId: "America/New_York" } }
  ]
];
