import assert from "node:assert/strict";
import { openHarness } from "../helpers/e2e-support.mjs";

async function testClientInfoBinding(page, baseUrl) {
  const requests = [];
  page.on("request", request => {
    if (request.url().includes("/__heimdall/v1/content/actions")) {
      requests.push({
        action: request.headers()["x-heimdall-content-action"],
        clientInfo: request.headers()["x-heimdall-client-info"]
      });
    }
  });

  await openHarness(page, baseUrl, "/e2e");
  await page.evaluate(() => {
    window.Heimdall.config.clientInfo = true;
    window.__heimdallClientInfoEvents = [];
    document.addEventListener("heimdall:client-info-before", event => {
      if (event.detail?.actionId !== "e2e.client-info")
        return;

      window.__heimdallClientInfoEvents.push({
        actionId: event.detail.actionId,
        requestId: event.detail.requestId,
        attempt: event.detail.attempt,
        sourceId: event.detail.sourceElement?.id || null
      });
      event.detail.info.locale = "en-HEIMDALL";
    });
  });

  await page.locator("#e2e-client-info-button").click();
  await page.locator("#e2e-client-info-result").waitFor();

  const request = requests.find(item => item.action === "e2e.client-info");
  assert.ok(request?.clientInfo, "Expected the client-information request header.");
  assert.ok(request.clientInfo.length < 4096);

  const info = JSON.parse(request.clientInfo);
  assert.ok(info.timeZone);
  assert.equal(info.locale, "en-HEIMDALL");
  assert.ok(info.screenWidth > 0);
  assert.ok(info.screenHeight > 0);
  assert.ok(["mobile", "tablet", "desktop"].includes(info.deviceCategory));
  assert.ok(["light", "dark", "no-preference"].includes(info.colorScheme));

  const expected = [
    "True",
    info.timeZone,
    info.locale,
    info.screenWidth,
    info.screenHeight,
    info.deviceCategory,
    info.colorScheme,
    info.online ? "True" : "False"
  ].join("|");

  assert.equal(
    (await page.locator("#e2e-client-info-result").textContent()).trim(),
    expected);

  const events = await page.evaluate(() => window.__heimdallClientInfoEvents);
  assert.equal(events.length, 1);
  assert.equal(events[0].actionId, "e2e.client-info");
  assert.ok(events[0].requestId > 0);
  assert.equal(events[0].attempt, 1);
  assert.equal(events[0].sourceId, "e2e-client-info-button");
}

export const tests = [
  ["binds opt-in browser information through the hosted action pipeline", testClientInfoBinding]
];
