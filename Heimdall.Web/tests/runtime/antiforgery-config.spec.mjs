import assert from "node:assert/strict";
import {
  actionFetches,
  csrfFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";

async function testDisabledAntiforgeryAction(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="antiforgery-result">Saved</span>' }]
  });

  const result = await page.evaluate(() => {
    window.Heimdall.config.antiforgery = false;
    document.body.innerHTML = '<div id="target">Old</div>';
    return window.Heimdall.invoke("Security.Unprotected", { id: 42 }, { target: "#target" });
  });

  assert.equal(result.ok, true);
  assert.equal(await page.locator("#target").innerHTML(), '<span id="antiforgery-result">Saved</span>');

  const fetches = await getFetches(page);
  const actions = actionFetches(fetches);
  assert.equal(csrfFetches(fetches).length, 0);
  assert.equal(actions.length, 1);
  assert.equal(actions[0].headers.requestverificationtoken, undefined);
  assert.deepEqual(actions[0].jsonBody, { id: 42 });
}

async function testDisabledAntiforgeryDoesNotRetry(page) {
  await installFakeServer(page, {
    actionResponses: [{ status: 400, body: "Invalid Heimdall antiforgery token." }]
  });

  const result = await page.evaluate(() => {
    window.Heimdall.config.antiforgery = false;
    return window.Heimdall.invoke("Security.Misconfigured", null, { swap: "none" });
  });

  assert.equal(result.ok, false);
  assert.equal(result.status, 400);

  const fetches = await getFetches(page);
  assert.equal(csrfFetches(fetches).length, 0);
  assert.equal(actionFetches(fetches).length, 1);
}

async function testDisabledAntiforgerySseToken(page) {
  await installFakeServer(page, {
    bifrostTokens: ["st-without-csrf"]
  });

  const eventSourceUrl = await page.evaluate(async () => {
    window.Heimdall.config.antiforgery = false;
    document.body.innerHTML = '<div id="sse-host"></div>';
    window.__antiforgeryEventSourceUrls = [];

    window.EventSource = class {
      constructor(url) {
        window.__antiforgeryEventSourceUrls.push(url);
      }

      addEventListener() {
      }

      close() {
      }
    };

    window.Heimdall.sse.connect("topic:no-csrf", {
      element: document.querySelector("#sse-host")
    });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        if (window.__antiforgeryEventSourceUrls.length > 0) {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for EventSource"));
        }
      }, 10);
    });

    return window.__antiforgeryEventSourceUrls[0];
  });

  const url = new URL(eventSourceUrl);
  assert.equal(url.searchParams.get("topic"), "topic:no-csrf");
  assert.equal(url.searchParams.get("st"), "st-without-csrf");

  const fetches = await getFetches(page);
  const tokenFetches = fetches.filter(fetch => fetch.url.includes("/__heimdall/v1/bifrost/token"));
  assert.equal(csrfFetches(fetches).length, 0);
  assert.equal(tokenFetches.length, 1);
  assert.equal(tokenFetches[0].headers.requestverificationtoken, undefined);
}

export const tests = [
  ["skips CSRF token calls and headers when antiforgery is disabled", testDisabledAntiforgeryAction],
  ["does not retry antiforgery failures when antiforgery is disabled", testDisabledAntiforgeryDoesNotRetry],
  ["mints SSE subscribe tokens without CSRF when antiforgery is disabled", testDisabledAntiforgerySseToken]
];
