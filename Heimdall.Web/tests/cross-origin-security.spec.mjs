import assert from "node:assert/strict";
import http from "node:http";
import { readFile } from "node:fs/promises";
import { chromium } from "playwright";

// Real origins and browser networking: no fetch or EventSource mocks. Server-side
// ASP.NET token validation is covered independently by FunctionsAntiforgeryScaleOutTests.
const bundle = await readFile(new URL("../wwwroot/heimdall-bundle.js", import.meta.url));
let shellOrigin;
let backendOrigin;
let generation = 0;
let invocationCount = 0;
let preflights = 0;
let streamCookie;
let exposeRejection = true;
let allowCors = true;
const streams = new Set();
const backend = http.createServer(async (req, res) => {
  if (allowCors) {
    res.setHeader("Access-Control-Allow-Origin", shellOrigin);
    res.setHeader("Access-Control-Allow-Credentials", "true");
    res.setHeader("Vary", "Origin");
    if (exposeRejection) res.setHeader("Access-Control-Expose-Headers", "X-Heimdall-Antiforgery");
  }
  if (req.method === "OPTIONS") {
    preflights++;
    res.setHeader("Access-Control-Allow-Methods", "GET, POST");
    res.setHeader("Access-Control-Allow-Headers", "Content-Type, X-Heimdall-Content-Invocation, X-Requested-With, RequestVerificationToken");
    res.writeHead(204).end(); return;
  }
  const url = new URL(req.url, backendOrigin);
  if (url.pathname.endsWith("/csrf")) {
    generation++;
    res.setHeader("Set-Cookie", "review-cookie=present; HttpOnly; SameSite=Lax; Path=/");
    res.setHeader("Cache-Control", "no-store");
    res.setHeader("Content-Type", "application/json");
    res.end(JSON.stringify({ requestToken: `token-${generation}` })); return;
  }
  if (url.pathname.endsWith("/bifrost")) {
    streamCookie = req.headers.cookie;
    res.writeHead(200, { "Content-Type": "text/event-stream", "Cache-Control": "no-cache" });
    res.write(": connected\n\n");
    streams.add(res);
    req.on("close", () => streams.delete(res)); return;
  }
  if (req.method === "POST") invocationCount++;
  if (!req.headers.cookie?.includes("review-cookie=present") || req.headers.requestverificationtoken !== `token-${generation}`) {
    res.writeHead(400, { "X-Heimdall-Antiforgery": "invalid" }).end("Rejected"); return;
  }
  if (url.pathname.endsWith("/bifrost/token")) {
    res.setHeader("Content-Type", "application/json");
    res.end(JSON.stringify({ token: "stream-ticket", expiresInSeconds: 120 })); return;
  }
  if (url.searchParams.get("invocation") === "business.error") {
    res.writeHead(400).end("The document contains the word csrf"); return;
  }
  res.setHeader("Content-Type", "text/html");
  res.end("<span>Saved</span>");
});
const shell = http.createServer((req, res) => {
  if (req.url === "/runtime.js") { res.setHeader("Content-Type", "text/javascript"); res.end(bundle); return; }
  res.setHeader("Content-Type", "text/html");
  res.end(`<!doctype html><script src="/runtime.js"></script><script>
    Object.assign(Heimdall.config.endpoints, {
      contentInvocations: '${backendOrigin}/__heimdall/v1/content/invocations',
      csrf: '${backendOrigin}/__heimdall/v1/csrf',
      bifrostToken: '${backendOrigin}/__heimdall/v1/bifrost/token',
      bifrost: '${backendOrigin}/__heimdall/v1/bifrost'
    });
    Heimdall.config.credentials = 'include';
  </script><div id="stream"></div>`);
});
const listen = server => new Promise(resolve => server.listen(0, "127.0.0.1", () => resolve(`http://127.0.0.1:${server.address().port}`)));
let browser;
try {
  backendOrigin = await listen(backend);
  shellOrigin = await listen(shell);
  browser = await chromium.launch();
  const page = await browser.newPage();
  await page.goto(shellOrigin);
  const invoke = id => page.evaluate(id => Heimdall.invoke(id, {}, { swap: "none" }), id);
  assert.equal((await invoke("save")).ok, true);
  assert.ok(preflights > 0);
  generation++; // Simulate a token invalidated by an identity/key change.
  let before = invocationCount;
  assert.equal((await invoke("save")).ok, true);
  assert.equal(invocationCount - before, 2);
  before = invocationCount;
  assert.equal((await invoke("business.error")).status, 400);
  assert.equal(invocationCount - before, 1);
  await page.evaluate(() => {
    window.streamOpened = false;
    document.addEventListener("heimdall:sse-open", () => window.streamOpened = true);
    Heimdall.sse.connect("review", { element: document.querySelector("#stream") });
  });
  await page.waitForFunction(() => window.streamOpened);
  assert.ok(streamCookie.includes("review-cookie=present"));
  exposeRejection = false;
  generation++;
  before = invocationCount;
  assert.equal((await invoke("save")).status, 400);
  assert.equal(invocationCount - before, 1, "Hidden rejection headers must not cause guessed retries");
  await page.goto(shellOrigin); // Full MPA navigation creates a fresh token cache.
  assert.equal((await invoke("save")).ok, true);
  allowCors = false;
  assert.equal((await invoke("save")).ok, false);
  console.log("ok - real cross-origin cookies, preflight, explicit CSRF refresh, no business-error replay, SSE credentials, MPA reset, and CORS denial");
} finally {
  await browser?.close();
  for (const stream of streams) stream.end();
  for (const server of [shell, backend]) {
    server.closeAllConnections();
    await new Promise(resolve => server.close(resolve));
  }
}
