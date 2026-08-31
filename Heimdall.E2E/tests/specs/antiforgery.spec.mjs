import assert from "node:assert/strict";
import {
  openHarness,
  waitForText
} from "../helpers/e2e-support.mjs";

async function testDisabledAntiforgeryAction(page, baseUrl) {
  const observedRequests = [];
  page.on("request", request => {
    observedRequests.push({
      url: request.url(),
      headers: request.headers()
    });
  });

  await openHarness(page, baseUrl, "/e2e");
  observedRequests.length = 0;

  await page.evaluate(() => {
    window.Heimdall.config.antiforgery = false;
  });

  await page.locator("#e2e-antiforgery-button").click();
  await waitForText(
    page.locator("#e2e-antiforgery-target"),
    "Antiforgery-disabled action completed");

  const csrfRequests = observedRequests.filter(request =>
    request.url.includes("/__heimdall/v1/csrf"));
  const actionRequest = observedRequests.find(request =>
    request.url.includes("/__heimdall/v1/content/actions") &&
    request.headers["x-heimdall-content-action"] === "e2e.antiforgery.disabled");

  assert.equal(csrfRequests.length, 0);
  assert.ok(actionRequest, "Expected the antiforgery-disabled content action request.");
  assert.equal(actionRequest.headers.requestverificationtoken, undefined);
}

export const tests = [
  [
    "runs an antiforgery-disabled action without a CSRF call or header",
    testDisabledAntiforgeryAction
  ]
];
