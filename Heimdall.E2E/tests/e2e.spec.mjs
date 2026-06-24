import assert from "node:assert/strict";
import fs from "node:fs";
import { createServer as createHttpServer } from "node:http";
import { spawn, spawnSync } from "node:child_process";
import { createRequire } from "node:module";
import { createServer as createNetServer } from "node:net";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const require = createRequire(import.meta.url);
const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const repoRoot = path.resolve(projectRoot, "..");
const appProject = path.join(projectRoot, "Heimdall.E2E.csproj");

const tests = [
  ["generates and serves static site output", testStaticSiteGeneration],
  ["boots the harness and runs load triggers", testHarnessBootAndLoad],
  ["boots dynamically swapped content and applies swap modes", testDynamicBootAndSwapModes],
  ["applies swaps, OOB, abort, and redirect directives", testResponseDirectives],
  ["binds state, forms, payload refs, and programmatic invokes", testPayloadsAndState],
  ["handles delegated input, change, keydown, blur, and hover events", testDelegatedEvents],
  ["honors scope, ignore, prevent-default, and disable modifiers", testEventBehaviorModifiers],
  ["invokes JavaScript before and after swaps", testJsInvokeVoid],
  ["receives deterministic Bifrost SSE updates, OOB, and JS directives", testHarnessSse],
  ["navigates cookie auth redirects from content actions", testAuthRedirectNavigation],
  [
    "returns detailed content action errors",
    testDetailedActionErrors,
    { allowedBrowserErrors: [/Failed to load resource: the server responded with a status of (404|500)/] }
  ]
];

function loadPlaywright() {
  try {
    return require("playwright");
  } catch {
    return require(path.join(repoRoot, "Heimdall.Web", "node_modules", "playwright"));
  }
}

async function getFreePort() {
  return new Promise((resolve, reject) => {
    const server = createNetServer();
    server.unref();
    server.on("error", reject);
    server.listen(0, "127.0.0.1", () => {
      const address = server.address();
      server.close(() => resolve(address.port));
    });
  });
}

function run(command, args, options = {}) {
  const result = spawnSync(command, args, {
    cwd: repoRoot,
    encoding: "utf8",
    shell: false,
    ...options
  });

  if (result.status !== 0) {
    throw new Error([
      `${command} ${args.join(" ")} failed with exit code ${result.status}.`,
      result.stdout || "",
      result.stderr || ""
    ].join("\n"));
  }

  return result.stdout || "";
}

function contentTypeFor(filePath) {
  switch (path.extname(filePath).toLowerCase()) {
    case ".css":
      return "text/css; charset=utf-8";
    case ".js":
      return "text/javascript; charset=utf-8";
    case ".json":
      return "application/json; charset=utf-8";
    case ".txt":
      return "text/plain; charset=utf-8";
    case ".xml":
      return "application/xml; charset=utf-8";
    case ".html":
    default:
      return "text/html; charset=utf-8";
  }
}

function resolveStaticRequestPath(root, pathBase, requestPath) {
  if (!requestPath.startsWith(pathBase))
    return null;

  let relativePath = requestPath.slice(pathBase.length);
  if (relativePath === "" || relativePath === "/")
    relativePath = "/index.html";
  else if (relativePath.endsWith("/"))
    relativePath += "index.html";

  const fullPath = path.resolve(root, `.${relativePath}`);
  const rootPath = path.resolve(root);
  if (fullPath !== rootPath && !fullPath.startsWith(`${rootPath}${path.sep}`))
    return null;

  if (fs.existsSync(fullPath) && fs.statSync(fullPath).isDirectory())
    return path.join(fullPath, "index.html");

  return fullPath;
}

async function withStaticFileServer(root, pathBase, callback) {
  const port = await getFreePort();
  const origin = `http://127.0.0.1:${port}`;
  const server = createHttpServer((req, res) => {
    const url = new URL(req.url || "/", origin);
    const filePath = resolveStaticRequestPath(root, pathBase, decodeURIComponent(url.pathname));

    if (!filePath || !fs.existsSync(filePath) || !fs.statSync(filePath).isFile()) {
      res.writeHead(404, { "Content-Type": "text/plain; charset=utf-8" });
      res.end("Not found");
      return;
    }

    res.writeHead(200, { "Content-Type": contentTypeFor(filePath) });
    fs.createReadStream(filePath).pipe(res);
  });

  await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(port, "127.0.0.1", resolve);
  });

  try {
    return await callback(origin);
  } finally {
    await new Promise(resolve => server.close(resolve));
  }
}

async function waitForHttpOk(url, timeoutMs = 30000) {
  const started = Date.now();
  let lastError = null;

  while (Date.now() - started < timeoutMs) {
    try {
      const res = await fetch(url);
      if (res.ok)
        return;
      lastError = new Error(`HTTP ${res.status}`);
    } catch (error) {
      lastError = error;
    }

    await new Promise(resolve => setTimeout(resolve, 150));
  }

  throw new Error(`Timed out waiting for ${url}. ${lastError ? lastError.message : ""}`.trim());
}

async function startApp() {
  if (process.env.HEIMDALL_E2E_SKIP_BUILD !== "1") {
    run("dotnet", ["build", appProject, "-v", "minimal"], { stdio: "inherit" });
  }

  const port = await getFreePort();
  const baseUrl = `http://127.0.0.1:${port}`;
  const output = [];

  const child = spawn("dotnet", [
    "run",
    "--no-build",
    "--no-launch-profile",
    "--project",
    appProject
  ], {
    cwd: repoRoot,
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: "Development",
      DOTNET_ENVIRONMENT: "Development",
      ASPNETCORE_URLS: baseUrl
    },
    stdio: ["ignore", "pipe", "pipe"]
  });

  child.stdout.on("data", chunk => output.push(String(chunk)));
  child.stderr.on("data", chunk => output.push(String(chunk)));

  child.on("exit", code => {
    if (code != null && code !== 0) {
      output.push(`\nHeimdall.E2E app exited with code ${code}.\n`);
    }
  });

  try {
    await waitForHttpOk(baseUrl);
  } catch (error) {
    stopApp(child);
    throw new Error(`${error.message}\n\nApp output:\n${output.slice(-30).join("")}`);
  }

  return {
    baseUrl,
    output,
    stop: () => stopApp(child)
  };
}

function stopApp(child) {
  if (!child || child.killed)
    return;

  try {
    child.kill();
  } catch {
    // ignore
  }
}

async function waitForText(locator, expected, timeoutMs = 5000) {
  const started = Date.now();
  while (Date.now() - started < timeoutMs) {
    const text = ((await locator.textContent().catch(() => "")) || "").trim();
    if (text === expected)
      return;

    await new Promise(resolve => setTimeout(resolve, 50));
  }

  assert.equal(((await locator.textContent().catch(() => "")) || "").trim(), expected);
}

function waitForActionAfter(page, actionId, timeoutMs = 8000) {
  return page.evaluate(({ actionId, timeoutMs }) => new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      document.removeEventListener("heimdall:after", handler);
      reject(new Error(`Timed out waiting for heimdall:after ${actionId}.`));
    }, timeoutMs);

    function handler(event) {
      if (!event.detail || event.detail.actionId !== actionId)
        return;

      clearTimeout(timer);
      document.removeEventListener("heimdall:after", handler);
      resolve(event.detail);
    }

    document.addEventListener("heimdall:after", handler);
  }), { actionId, timeoutMs });
}

async function openHarness(page, baseUrl, pathName = "/") {
  await page.goto(`${baseUrl}${pathName}`);
  await page.locator("#e2e-harness").waitFor();
}

async function runTests() {
  const { chromium } = loadPlaywright();
  const app = await startApp();
  const browser = await chromium.launch({ headless: true });

  try {
    for (const [name, fn, options = {}] of tests) {
      const context = await browser.newContext();
      const page = await context.newPage();
      page.setDefaultTimeout(8000);
      const browserErrors = [];

      page.on("pageerror", error => {
        browserErrors.push(error && error.message ? error.message : String(error));
      });

      page.on("console", message => {
        if (message.type() === "error")
          browserErrors.push(message.text());
      });

      try {
        await fn(page, app.baseUrl);
        const allowed = options.allowedBrowserErrors || [];
        const unexpectedErrors = browserErrors.filter(error => {
          return !allowed.some(pattern => pattern.test(error));
        });
        assert.deepEqual(unexpectedErrors, [], "Browser should not emit unexpected console or page errors.");
        console.log(`ok - ${name}`);
      } catch (error) {
        console.error(`not ok - ${name}`);
        console.error(error && error.stack ? error.stack : error);
        console.error(`\nApp output:\n${app.output.slice(-30).join("")}`);
        throw error;
      } finally {
        await context.close();
      }
    }
  } finally {
    await browser.close();
    app.stop();
  }

  console.log(`Heimdall E2E tests passed (${tests.length} checks).`);
}

async function testStaticSiteGeneration(page) {
  const outputPath = fs.mkdtempSync(path.join(os.tmpdir(), "heimdall-e2e-static-"));
  const stalePath = path.join(outputPath, "stale.txt");
  fs.writeFileSync(stalePath, "stale");

  try {
    const stdout = run("dotnet", [
      "run",
      "--no-build",
      "--no-launch-profile",
      "--project",
      appProject,
      "--",
      "--heimdall-generate-static"
    ], {
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: "Development",
        DOTNET_ENVIRONMENT: "Development",
        HEIMDALL_E2E_STATIC_OUTPUT: outputPath
      }
    });

    assert.match(stdout, /Generated 5 page\(s\) and \d+ asset\(s\)\./);
    assert.equal(fs.existsSync(stalePath), false, "CleanOutputPath should remove stale output files.");

    const indexPath = path.join(outputPath, "index.html");
    const e2ePath = path.join(outputPath, "e2e", "index.html");
    const docsPath = path.join(outputPath, "docs", "start", "index.html");
    const feedPath = path.join(outputPath, "feed.xml");
    const notFoundPath = path.join(outputPath, "404.html");
    const sitemapPath = path.join(outputPath, "sitemap.xml");
    const robotsPath = path.join(outputPath, "robots.txt");
    const manifestPath = path.join(outputPath, "heimdall.static.manifest.json");
    const appCssPath = path.join(outputPath, "css", "app.css");
    const heimdallBundlePath = path.join(outputPath, "_content", "HeimdallFramework.Web", "heimdall-bundle.min.js");

    for (const filePath of [
      indexPath,
      e2ePath,
      docsPath,
      feedPath,
      notFoundPath,
      sitemapPath,
      robotsPath,
      manifestPath,
      appCssPath,
      heimdallBundlePath
    ]) {
      assert.equal(fs.existsSync(filePath), true, `${filePath} should exist.`);
    }

    const indexHtml = fs.readFileSync(indexPath, "utf8");
    assert.match(indexHtml, /id="static-page"/);
    assert.match(indexHtml, /data-route="\/"/);
    assert.match(indexHtml, /href="\/e2e-static\/css\/app\.css"/);
    assert.match(indexHtml, /src="\/e2e-static\/_content\/HeimdallFramework\.Web\/heimdall-bundle\.min\.js"/);

    const docsHtml = fs.readFileSync(docsPath, "utf8");
    assert.match(docsHtml, /Static Docs/);
    assert.match(docsHtml, /data-route="\/docs\/start"/);

    assert.match(fs.readFileSync(feedPath, "utf8"), /<feed><title>Heimdall E2E<\/title><\/feed>/);
    assert.match(fs.readFileSync(notFoundPath, "utf8"), /data-kind="not-found"/);

    const sitemap = fs.readFileSync(sitemapPath, "utf8");
    assert.match(sitemap, /https:\/\/heimdall-e2e\.example\/e2e-static\//);
    assert.match(sitemap, /https:\/\/heimdall-e2e\.example\/e2e-static\/docs\/start\//);
    assert.doesNotMatch(sitemap, /feed\.xml/);
    assert.doesNotMatch(sitemap, /404\.html/);

    const robots = fs.readFileSync(robotsPath, "utf8");
    assert.match(robots, /Allow: \/e2e-static\//);
    assert.match(robots, /Sitemap: https:\/\/heimdall-e2e\.example\/e2e-static\/sitemap\.xml/);

    const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
    assert.equal(manifest.schemaVersion, 1);
    assert.equal(manifest.manifestRelativePath, "heimdall.static.manifest.json");

    const pageRoutes = manifest.files
      .filter(file => file.kind === "page")
      .map(file => file.route)
      .sort();
    assert.deepEqual(pageRoutes, ["/", "/404.html", "/docs/start", "/e2e", "/feed.xml"]);

    const relativePaths = manifest.files.map(file => file.relativePath);
    assert.ok(relativePaths.includes("css/app.css"));
    assert.ok(relativePaths.includes("_content/HeimdallFramework.Web/heimdall-bundle.min.js"));
    assert.ok(relativePaths.includes("sitemap.xml"));
    assert.ok(relativePaths.includes("robots.txt"));

    await withStaticFileServer(outputPath, "/e2e-static", async origin => {
      await page.goto(`${origin}/e2e-static/`);
      await page.locator("#static-page").waitFor();
      assert.equal(await page.locator("#static-page").getAttribute("data-route"), "/");
      assert.equal(await page.locator("#static-page").getAttribute("data-path-base"), "/e2e-static");
      assert.equal(await page.locator("#static-docs-link").getAttribute("href"), "/e2e-static/docs/start/");

      await page.goto(`${origin}/e2e-static/docs/start/`);
      await page.locator("#static-page").waitFor();
      assert.equal(await page.locator("#static-page").getAttribute("data-route"), "/docs/start");
      await page.getByRole("heading", { name: "Static Docs" }).waitFor();

      const bundleResponse = await page.request.get(`${origin}/e2e-static/_content/HeimdallFramework.Web/heimdall-bundle.min.js`);
      assert.equal(bundleResponse.ok(), true);
    });
  } finally {
    fs.rmSync(outputPath, { recursive: true, force: true });
  }
}

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

async function testDynamicBootAndSwapModes(page, baseUrl) {
  await openHarness(page, baseUrl, "/e2e");

  await page.locator("#e2e-dynamic-button").click();
  await waitForText(page.locator("#e2e-dynamic-load-target"), "Dynamic load completed");

  await page.locator("#e2e-outer-button").click();
  await waitForText(page.locator("#e2e-outer-target"), "Outer swapped");
  assert.equal(await page.locator("#e2e-outer-result").count(), 1);

  await page.locator("#e2e-beforeend-button").click();
  await page.locator("#e2e-beforeend-result").waitFor();
  assert.deepEqual(await page.locator("#e2e-beforeend-target > span").evaluateAll(nodes => nodes.map(node => node.id)), [
    "e2e-beforeend-original",
    "e2e-beforeend-result"
  ]);

  await page.locator("#e2e-afterbegin-button").click();
  await page.locator("#e2e-afterbegin-result").waitFor();
  assert.deepEqual(await page.locator("#e2e-afterbegin-target > span").evaluateAll(nodes => nodes.map(node => node.id)), [
    "e2e-afterbegin-result",
    "e2e-afterbegin-original"
  ]);

  const noneCompleted = waitForActionAfter(page, "e2e.swap.none");
  await page.locator("#e2e-none-button").click();
  await noneCompleted;
  await waitForText(page.locator("#e2e-none-target"), "None target original");
  assert.equal(await page.locator("#e2e-none-result").count(), 0);
}

async function testResponseDirectives(page, baseUrl) {
  await page.addInitScript(() => {
    window.__heimdallE2EAbortReasons = [];
    document.addEventListener("heimdall:abort", event => {
      window.__heimdallE2EAbortReasons.push(event.detail && event.detail.reason);
    });
  });

  await openHarness(page, baseUrl, "/e2e");

  await page.locator("#e2e-swap-button").click();
  await waitForText(page.locator("#e2e-swap-target"), "Swap completed");

  await page.locator("#e2e-oob-button").click();
  await waitForText(page.locator("#e2e-oob-main-target"), "OOB main completed");
  await waitForText(page.locator("#e2e-oob-side-target"), "OOB side completed");

  await page.locator("#e2e-abort-button").click();
  await waitForText(page.locator("#e2e-abort-target"), "Abort target original");
  await waitForText(page.locator("#e2e-abort-side"), "Abort side completed");
  await page.waitForFunction(() => (window.__heimdallE2EAbortReasons || []).includes("e2e-abort"));

  await page.locator("#e2e-redirect-button").click();
  await page.waitForFunction(() => window.location.hash === "#e2e-redirected");
  await waitForText(page.locator("#e2e-redirect-target"), "Redirect target original");
}

async function testPayloadsAndState(page, baseUrl) {
  await openHarness(page, baseUrl, "/e2e");

  const count = page.locator("#e2e-state-count");
  await waitForText(count, "0");
  await page.locator("#e2e-state-increment").click();
  await waitForText(count, "1");
  await page.locator("#e2e-state-increment").click();
  await waitForText(count, "2");
  await page.locator("#e2e-state-decrement").click();
  await waitForText(count, "1");
  await page.locator("#e2e-state-reset").click();
  await waitForText(count, "0");

  const invokeResult = await page.evaluate(async () => {
    const result = await window.Heimdall.invoke(
      "e2e.programmatic",
      { message: "from invoke" },
      { target: "#e2e-programmatic-target" });
    return { ok: result.ok, status: result.status };
  });
  assert.deepEqual(invokeResult, { ok: true, status: 200 });
  await waitForText(page.locator("#e2e-programmatic-target"), "Programmatic: from invoke");

  await page.evaluate(() => window.HeimdallE2E.setPayload("from ref"));
  await page.locator("#e2e-payload-ref-button").click();
  await waitForText(page.locator("#e2e-payload-ref-target"), "Payload ref: from ref");

  await page.locator("#e2e-self-payload-button").click();
  await waitForText(page.locator("#e2e-self-payload-target"), "Self payload: from self");

  await page.locator("#e2e-form-submit").click();
  await waitForText(page.locator("#e2e-form-result"), "Name is required.");
  await page.locator("#e2e-name").fill("Ada");
  await page.locator("#e2e-form-submit").click();
  await waitForText(page.locator("#e2e-form-result"), "Hello, Ada.");
}

async function testDelegatedEvents(page, baseUrl) {
  await openHarness(page, baseUrl, "/e2e");

  await page.locator("#e2e-input").fill("typed");
  await waitForText(page.locator("#e2e-input-result"), "Input: typed");

  await page.locator("#e2e-change").selectOption("beta");
  await waitForText(page.locator("#e2e-change-result"), "Choice: beta");

  await page.locator("#e2e-key-input").fill("enter text");
  await page.locator("#e2e-key-input").press("Enter");
  await waitForText(page.locator("#e2e-key-result"), "Key: enter text");

  await page.locator("#e2e-blur-input").fill("blurred");
  await page.locator("#e2e-blur-input").blur();
  await waitForText(page.locator("#e2e-blur-result"), "Blur: blurred");

  await page.locator("#e2e-hover-trigger").hover();
  await waitForText(page.locator("#e2e-hover-result"), "Hover completed");
}

async function testEventBehaviorModifiers(page, baseUrl) {
  await openHarness(page, baseUrl, "/e2e");
  const result = page.locator("#e2e-behavior-result");

  await waitForText(result, "Behavior target original");
  await page.locator("#e2e-scope-self-child").click();
  await page.waitForTimeout(200);
  await waitForText(result, "Behavior target original");

  await page.locator("#e2e-scope-self-trigger").click({ position: { x: 6, y: 6 } });
  await waitForText(result, "Marker: scope self");

  await page.locator("#e2e-ignore-child").click();
  await page.waitForTimeout(200);
  await waitForText(result, "Marker: scope self");

  await page.locator("#e2e-ignore-parent").click({ position: { x: 6, y: 6 } });
  await waitForText(result, "Marker: ignore parent");

  await page.locator("#e2e-prevent-link").click();
  await waitForText(result, "Marker: prevented link");
  assert.notEqual(await page.evaluate(() => window.location.hash), "#e2e-should-not-change");

  const disableButton = page.locator("#e2e-disable-button");
  await disableButton.click();
  await page.waitForFunction(() => document.querySelector("#e2e-disable-button")?.hasAttribute("disabled") === true);
  await waitForText(page.locator("#e2e-disable-result"), "Disable completed");
  await page.waitForFunction(() => document.querySelector("#e2e-disable-button")?.hasAttribute("disabled") === false);
}

async function testJsInvokeVoid(page, baseUrl) {
  await openHarness(page, baseUrl, "/e2e");

  await page.locator("#e2e-js-button").click();
  await waitForText(page.locator("#e2e-js-target"), "JS target swapped");
  await waitForText(page.locator("#e2e-js-log"), "before:ok:JS target original|after:ok:JS target swapped");

  assert.deepEqual(await page.evaluate(() => window.HeimdallE2E.calls), [
    { phase: "before", value: "ok", targetText: "JS target original" },
    { phase: "after", value: "ok", targetText: "JS target swapped" }
  ]);
}

async function testHarnessSse(page, baseUrl) {
  await page.addInitScript(() => {
    window.__heimdallE2ESseOpen = false;
    document.addEventListener("heimdall:sse-open", event => {
      if (event.detail && event.detail.topic === "e2e-harness")
        window.__heimdallE2ESseOpen = true;
    });
  });

  await openHarness(page, baseUrl, "/e2e");
  await page.waitForFunction(() => window.__heimdallE2ESseOpen === true, null, { timeout: 8000 });

  await page.locator("#e2e-sse-button").click();
  await waitForText(page.locator("#e2e-sse-target"), "SSE delivered", 10000);

  await page.locator("#e2e-sse-oob-button").click();
  await waitForText(page.locator("#e2e-sse-target"), "SSE OOB main", 10000);
  await waitForText(page.locator("#e2e-sse-oob-target"), "SSE OOB side", 10000);

  await page.locator("#e2e-sse-js-button").click();
  await waitForText(page.locator("#e2e-sse-target"), "SSE JS main", 10000);
  await waitForText(page.locator("#e2e-sse-js-log"), "sse-ok:SSE JS main:1", 10000);
  assert.deepEqual(await page.evaluate(() => window.HeimdallE2E.sseCalls), [
    { value: "sse-ok", targetText: "SSE JS main" }
  ]);
}

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
      url.searchParams.get("ReturnUrl")?.includes("/__heimdall/v1/content/actions");
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
  assert.equal(redirectState.targetText, "Auth target original");
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

await runTests();
