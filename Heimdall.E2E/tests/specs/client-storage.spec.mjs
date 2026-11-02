import assert from "node:assert/strict";
import {
  openHarness,
  waitForActionAfter,
  waitForText
} from "../helpers/e2e-support.mjs";

async function testClientStoragePayloadAndResponses(page, baseUrl) {
  await openHarness(page, baseUrl, "/e2e");

  await page.evaluate(() => {
    localStorage.setItem("e2e.payload", JSON.stringify({ message: "from local storage" }));
  });
  await page.locator("#e2e-storage-payload-button").click();
  await waitForText(page.locator("#e2e-storage-payload-target"), "Storage payload: from local storage");

  await page.locator("#e2e-storage-directive-button").click();
  await waitForText(page.locator("#e2e-storage-directive-target"), "Storage directive applied");
  assert.equal(await page.evaluate(() => localStorage.getItem("e2e.theme")), "dark");
  assert.equal(await page.evaluate(() => sessionStorage.getItem("e2e.checkout")), "open");
  assert.equal(await page.locator("storage").count(), 0);

  const abortCompleted = waitForActionAfter(page, "e2e.storage.abort");
  await page.locator("#e2e-storage-abort-button").click();
  await abortCompleted;
  assert.equal(await page.evaluate(() => localStorage.getItem("e2e.abort")), "kept");
  assert.equal((await page.locator("#e2e-storage-abort-target").textContent()).trim(), "Storage abort target original");
}

async function testClientStorageAllowlist(page, baseUrl) {
  const requests = [];
  page.on("request", request => {
    if (request.url().includes("/__heimdall/v1/content/actions") &&
        request.headers()["x-heimdall-content-action"] === "e2e.client-info") {
      requests.push(request.headers()["x-heimdall-client-info"]);
    }
  });

  await openHarness(page, baseUrl, "/e2e");
  await page.evaluate(() => {
    localStorage.setItem("e2e.theme", "dark");
    localStorage.setItem("e2e.ignored", "not configured");
    sessionStorage.setItem("e2e.checkout", "open");
    window.Heimdall.config.clientInfo = {
      enabled: true,
      storage: {
        local: ["e2e.theme"],
        session: ["e2e.checkout"]
      }
    };
  });

  await page.locator("#e2e-client-info-button").click();
  await page.locator("#e2e-client-info-result").waitFor();
  assert.equal(requests.length, 1);
  const info = JSON.parse(requests[0]);
  assert.deepEqual(info.storage, {
    local: { "e2e.theme": "dark" },
    session: { "e2e.checkout": "open" }
  });
  assert.equal(info.storage.local["e2e.ignored"], undefined);
}

async function testClientStorageSse(page, baseUrl) {
  await page.addInitScript(() => {
    window.__heimdallE2EStorageSseOpen = false;
    document.addEventListener("heimdall:sse-open", event => {
      if (event.detail?.topic === "e2e-harness")
        window.__heimdallE2EStorageSseOpen = true;
    });
  });

  await openHarness(page, baseUrl, "/e2e");
  await page.waitForFunction(() => window.__heimdallE2EStorageSseOpen === true, null, { timeout: 8000 });
  await page.locator("#e2e-storage-sse-button").click();
  await waitForText(page.locator("#e2e-sse-target"), "Storage SSE delivered", 10000);
  assert.equal(await page.evaluate(() => localStorage.getItem("e2e.sse")), "delivered");
  assert.equal(await page.locator("storage").count(), 0);
}

export const tests = [
  ["uses storage payloads and response directives in the hosted action pipeline", testClientStoragePayloadAndResponses],
  ["sends only configured storage values in hosted client information", testClientStorageAllowlist],
  ["applies storage directives delivered over Bifrost SSE", testClientStorageSse]
];
