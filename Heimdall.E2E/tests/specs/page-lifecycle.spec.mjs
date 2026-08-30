import assert from "node:assert/strict";
import {
  openHarness,
  waitForActionAfter,
  waitForText
} from "../helpers/e2e-support.mjs";

const documentVisibleAction = "e2e.document-visible";
const onlineAction = "e2e.online";

async function testPageLifecycleTriggersAndOfflineEvent(page, baseUrl) {
  await page.addInitScript(() => {
    window.__heimdallTestVisibility = "visible";
    Object.defineProperty(document, "visibilityState", {
      configurable: true,
      get: () => window.__heimdallTestVisibility
    });
  });
  await openHarness(page, baseUrl, "/e2e");

  assert.equal(
    await page.locator("#e2e-document-visible-trigger").getAttribute("heimdall-content-document-visible"),
    documentVisibleAction
  );
  assert.equal(
    await page.locator("#e2e-online-trigger").getAttribute("heimdall-content-online"),
    onlineAction
  );
  await waitForText(page.locator("#e2e-document-visible-target"), "Document visible target original");
  await waitForText(page.locator("#e2e-online-target"), "Online target original");

  await page.evaluate(() => {
    window.__heimdallLifecycleActions = [];
    document.addEventListener("heimdall:before", event => {
      window.__heimdallLifecycleActions.push(event.detail?.actionId || null);
    });
  });

  await page.context().setOffline(true);
  await waitForText(page.locator("#e2e-offline-result"), "Offline events: 1");
  await page.waitForTimeout(100);
  assert.deepEqual(await page.evaluate(() => window.__heimdallLifecycleActions), []);
  await waitForText(page.locator("#e2e-online-target"), "Online target original");

  const onlineCompleted = waitForActionAfter(page, onlineAction);
  await page.context().setOffline(false);
  await onlineCompleted;
  await waitForText(page.locator("#e2e-online-target"), "Online completed");

  await page.evaluate(() => {
    window.__heimdallTestVisibility = "hidden";
    document.dispatchEvent(new Event("visibilitychange"));
  });
  await page.waitForTimeout(100);
  await waitForText(page.locator("#e2e-document-visible-target"), "Document visible target original");

  const visibleCompleted = waitForActionAfter(page, documentVisibleAction);
  await page.evaluate(() => {
    window.__heimdallTestVisibility = "visible";
    document.dispatchEvent(new Event("visibilitychange"));
  });
  await visibleCompleted;
  await waitForText(page.locator("#e2e-document-visible-target"), "Document visible completed");

  await page.evaluate(() => {
    window.__heimdallTestVisibility = "hidden";
    document.dispatchEvent(new Event("visibilitychange"));
  });
  const visibleAgain = waitForActionAfter(page, documentVisibleAction);
  await page.evaluate(() => {
    window.__heimdallTestVisibility = "visible";
    document.dispatchEvent(new Event("visibilitychange"));
  });
  await visibleAgain;

  assert.deepEqual(await page.evaluate(() => window.__heimdallLifecycleActions), [
    onlineAction,
    documentVisibleAction,
    documentVisibleAction
  ]);
}

export const tests = [
  ["handles online and repeated document-visible actions while offline stays client-only", testPageLifecycleTriggersAndOfflineEvent]
];
