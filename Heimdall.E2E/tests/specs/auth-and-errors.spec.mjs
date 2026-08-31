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

async function testAuthRedirectNavigation(page, baseUrl) {
  await openHarness(page, baseUrl, "/e2e");
  await waitForText(page.locator("#e2e-auth-target"), "Auth target original");

  await page.evaluate(() => {
    localStorage.removeItem("heimdall-e2e-auth-action");
    localStorage.removeItem("heimdall-e2e-auth-redirect-url");
    localStorage.removeItem("heimdall-e2e-auth-target-text");

    document.addEventListener("heimdall:redirect", event => {
      const detail = event.detail || {};
      if (detail.actionId !== "e2e.auth.required")
        return;

      const target = document.querySelector("#e2e-auth-target");
      localStorage.setItem("heimdall-e2e-auth-action", detail.actionId || "");
      localStorage.setItem("heimdall-e2e-auth-redirect-url", detail.url || "");
      localStorage.setItem("heimdall-e2e-auth-target-text", target ? target.textContent.trim() : "");
    });
  });

  const navigation = page.waitForURL(url => {
    return url.pathname === "/e2e-signin" &&
      url.searchParams.get("ReturnUrl") === "/e2e";
  });

  await page.locator("#e2e-auth-button").click();
  await navigation;
  await waitForText(page.locator("#e2e-signin-page"), "Sign in required");

  const redirectState = await page.evaluate(() => ({
    actionId: localStorage.getItem("heimdall-e2e-auth-action"),
    redirectUrl: localStorage.getItem("heimdall-e2e-auth-redirect-url"),
    targetText: localStorage.getItem("heimdall-e2e-auth-target-text")
  }));

  assert.equal(redirectState.actionId, "e2e.auth.required");
  assert.match(redirectState.redirectUrl, /\/e2e-signin\?ReturnUrl=/);
  assert.equal(new URL(redirectState.redirectUrl).searchParams.get("ReturnUrl"), "/e2e");
  assert.equal(redirectState.targetText, "Auth target original");
}

async function testSseAuthRedirectNavigation(page, baseUrl) {
  await openHarness(page, baseUrl, "/e2e");

  await page.evaluate(() => {
    localStorage.removeItem("heimdall-e2e-sse-auth-topic");
    localStorage.removeItem("heimdall-e2e-sse-auth-redirect-url");

    document.addEventListener("heimdall:sse-redirect", event => {
      const detail = event.detail || {};
      if (detail.topic !== "e2e-auth-required")
        return;

      localStorage.setItem("heimdall-e2e-sse-auth-topic", detail.topic || "");
      localStorage.setItem("heimdall-e2e-sse-auth-redirect-url", detail.redirectUrl || "");
    });

    window.Heimdall.sse.connect("e2e-auth-required", {
      element: document.querySelector("#e2e-harness"),
      event: "message"
    });
  });

  await page.waitForURL(url => {
    return url.pathname === "/e2e-signin" &&
      url.searchParams.get("ReturnUrl") === "/e2e";
  });
  await waitForText(page.locator("#e2e-signin-page"), "Sign in required");

  const redirectState = await page.evaluate(() => ({
    topic: localStorage.getItem("heimdall-e2e-sse-auth-topic"),
    redirectUrl: localStorage.getItem("heimdall-e2e-sse-auth-redirect-url")
  }));

  assert.equal(redirectState.topic, "e2e-auth-required");
  assert.match(redirectState.redirectUrl, /\/e2e-signin\?ReturnUrl=/);
  assert.equal(new URL(redirectState.redirectUrl).searchParams.get("ReturnUrl"), "/e2e");
}

async function testDetailedActionErrors(page, baseUrl) {
  await openHarness(page, baseUrl, "/e2e");

  const result = await page.evaluate(async () => {
    const errorEvent = new Promise(resolve => {
      document.addEventListener("heimdall:error", event => {
        resolve({
          status: event.detail && event.detail.status,
          body: event.detail && event.detail.body
        });
      }, { once: true });
    });

    const actionResult = await window.Heimdall.invoke(
      "e2e.error",
      {},
      { target: "#e2e-error-target" });

    return {
      ok: actionResult.ok,
      status: actionResult.status,
      error: actionResult.error,
      event: await errorEvent
    };
  });

  assert.equal(result.ok, false);
  assert.equal(result.status, 500);
  assert.match(result.error, /E2E expected failure/);
  assert.equal(result.event.status, 500);
  assert.match(result.event.body, /E2E expected failure/);
  await waitForText(page.locator("#e2e-error-target"), "Error target original");

  const missing = await page.evaluate(async () => {
    const errorEvent = new Promise(resolve => {
      document.addEventListener("heimdall:error", event => {
        resolve({
          status: event.detail && event.detail.status,
          body: event.detail && event.detail.body
        });
      }, { once: true });
    });

    const actionResult = await window.Heimdall.invoke(
      "e2e.missing",
      {},
      { target: "#e2e-error-target" });

    return {
      ok: actionResult.ok,
      status: actionResult.status,
      error: actionResult.error,
      event: await errorEvent
    };
  });

  assert.equal(missing.ok, false);
  assert.equal(missing.status, 404);
  assert.match(missing.error, /Unknown action 'e2e\.missing'/);
  assert.equal(missing.event.status, 404);
  assert.match(missing.event.body, /Unknown action 'e2e\.missing'/);
  await waitForText(page.locator("#e2e-error-target"), "Error target original");
}

export const tests = [
  ["navigates cookie auth redirects from content actions", testAuthRedirectNavigation],
  [
    "navigates cookie auth redirects from SSE token fetches",
    testSseAuthRedirectNavigation,
    { allowedBrowserErrors: [/Failed to load resource: the server responded with a status of (401|403)/] }
  ],
  [
    "returns detailed content action errors",
    testDetailedActionErrors,
    { allowedBrowserErrors: [/Failed to load resource: the server responded with a status of (404|500)/] }
  ]
];
