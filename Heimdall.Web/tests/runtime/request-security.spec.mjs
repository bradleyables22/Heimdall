import assert from "node:assert/strict";
import { installFakeServer, getFetches, actionFetches, csrfFetches } from "../helpers/runtime-page.mjs";

async function testBusinessError(page) {
  await installFakeServer(page, { actionResponses: [{ status: 400, body: "Document contains reserved word csrf or antiforgery" }] });
  const result = await page.evaluate(() => Heimdall.invoke("document.save", {}, { swap: "none" }));
  assert.equal(result.status, 400);
  assert.equal(actionFetches(await getFetches(page)).length, 1);
}

async function testBoundedRetry(page) {
  await installFakeServer(page, {
    csrfTokens: ["first", "second"],
    actionResponses: Array.from({ length: 3 }, () => ({ status: 400, headers: { "X-Heimdall-Antiforgery": "invalid" }, body: "Rejected" }))
  });
  const result = await page.evaluate(() => Heimdall.invoke("document.save", {}, { swap: "none" }));
  assert.equal(result.status, 400);
  const fetches = await getFetches(page);
  const requests = actionFetches(fetches);
  assert.equal(requests.length, 2);
  assert.equal(csrfFetches(fetches).length, 2);
  assert.equal(requests[0].headers.requestverificationtoken, "first");
  assert.equal(requests[1].headers.requestverificationtoken, "second");
}

async function testCredentials(page, credentials) {
  await installFakeServer(page);
  await page.evaluate(async credentials => {
    Heimdall.config.credentials = credentials;
    const root = "https://backend.example/__heimdall/v1/";
    for (const [key, route] of Object.entries({ contentInvocations: "content/invocations", csrf: "csrf", bifrostToken: "bifrost/token", bifrost: "bifrost" }))
      Heimdall.config.endpoints[key] = root + route;
    window.__stream = null;
    window.EventSource = class {
      constructor(url, options) { window.__stream = { url, options }; }
      addEventListener() {}
      close() {}
    };
    await Heimdall.invoke("document.save", {}, { swap: "none" });
    document.body.innerHTML = '<div id="stream"></div>';
    Heimdall.sse.connect("updates", { element: document.querySelector("#stream") });
  }, credentials);
  await page.waitForFunction(() => !!window.__stream);
  const fetches = await getFetches(page);
  assert.equal(fetches.length, 3);
  for (const request of fetches) {
    assert.equal(request.credentials, credentials);
    assert.equal(new URL(request.url).origin, "https://backend.example");
  }
  const stream = await page.evaluate(() => window.__stream);
  assert.equal(stream.options.withCredentials, credentials === "include");
  assert.equal(new URL(stream.url).origin, "https://backend.example");
}

async function testConcurrentRefresh(page) {
  await installFakeServer(page, {
    csrfTokens: ["old", "new"],
    actionResponses: [
      { status: 400, headers: { "X-Heimdall-Antiforgery": "invalid" }, delayMs: 10 },
      { status: 400, headers: { "X-Heimdall-Antiforgery": "invalid" }, delayMs: 60 },
      { status: 200 }, { status: 200 }
    ]
  });
  const results = await page.evaluate(() => Promise.all([
    Heimdall.invoke("first", {}, { swap: "none" }),
    Heimdall.invoke("second", {}, { swap: "none" })
  ]));
  assert.ok(results.every(result => result.ok));
  assert.equal(csrfFetches(await getFetches(page)).length, 2, "Late rejection of the old token must not discard the refreshed token");
}

export const tests = [
  ["request security shares refresh across concurrent old-token failures", testConcurrentRefresh],
  ["request security does not replay business errors mentioning csrf", testBusinessError],
  ["request security retries only one explicit rejection", testBoundedRetry],
  ["request security defaults to same-origin credentials", page => testCredentials(page, "same-origin")],
  ["request security includes credentials consistently for a remote backend", page => testCredentials(page, "include")]
];
