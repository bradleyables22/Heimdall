import assert from "node:assert/strict";
import { openHarness, waitForText } from "../helpers/e2e-support.mjs";

async function waitForDocumentLoadCount(page, minimum) {
  await page.waitForFunction(expected => Number(window.__heimdallDocumentLoadCount || 0) >= expected, minimum);
}

async function testHistoryPushReplaceAndBrowserTraversal(page, baseUrl) {
  await page.addInitScript(() => {
    const next = Number(sessionStorage.getItem("heimdall-history-load-count") || 0) + 1;
    sessionStorage.setItem("heimdall-history-load-count", String(next));
    window.__heimdallDocumentLoadCount = next;
  });

  await openHarness(page, baseUrl, "/e2e");
  const initialLength = await page.evaluate(() => history.length);

  await page.locator("#e2e-history-push-button").click();
  await page.waitForURL(`${baseUrl}/history/pushed`);
  await waitForText(page.locator("#e2e-history-target"), "History pushed");
  assert.equal(await page.evaluate(() => history.length), initialLength + 1);
  assert.equal(await page.evaluate(() => window.__heimdallDocumentLoadCount), 1, "pushState must not reload immediately.");

  await page.goBack({ waitUntil: "domcontentloaded" });
  await page.waitForURL(`${baseUrl}/e2e`);
  await waitForDocumentLoadCount(page, 2);
  await page.locator("#e2e-harness").waitFor();

  await page.goForward({ waitUntil: "domcontentloaded" });
  await page.waitForURL(`${baseUrl}/history/pushed`);
  await waitForDocumentLoadCount(page, 3);
  await page.locator("#e2e-harness").waitFor();

  const lengthBeforeReplace = await page.evaluate(() => history.length);
  await page.locator("#e2e-history-replace-button").click();
  await page.waitForURL(`${baseUrl}/history/replaced`);
  await waitForText(page.locator("#e2e-history-target"), "History replaced");
  assert.equal(await page.evaluate(() => history.length), lengthBeforeReplace);
  assert.equal(await page.evaluate(() => window.__heimdallDocumentLoadCount), 3, "replaceState must not reload immediately.");

  await page.goBack({ waitUntil: "domcontentloaded" });
  await page.waitForURL(`${baseUrl}/e2e`);
  await waitForDocumentLoadCount(page, 4);

  await page.goForward({ waitUntil: "domcontentloaded" });
  await page.waitForURL(`${baseUrl}/history/replaced`);
  await waitForDocumentLoadCount(page, 5);
}

export const tests = [
  ["pushes, replaces, and fully reloads canonical history routes on browser traversal", testHistoryPushReplaceAndBrowserTraversal]
];
