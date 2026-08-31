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
export const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
export const repoRoot = path.resolve(projectRoot, "..");
export const appProject = path.join(projectRoot, "Heimdall.E2E.csproj");

export const expectedLocalTimeFormats = {
  "standard-d": "8/6/26",
  "standard-D": "Thursday, August 6, 2026",
  "standard-t": "4:05 AM",
  "standard-T": "4:05:07 AM",
  "standard-g": "8/6/26, 4:05 AM",
  "standard-G": "8/6/26, 4:05:07 AM",
  "day-1": "6",
  "day-2": "06",
  "day-3": "Thu",
  "day-4": "Thursday",
  "month-1": "8",
  "month-2": "08",
  "month-3": "Aug",
  "month-4": "August",
  "year-1": "26",
  "year-2": "26",
  "year-3": "2026",
  "year-4": "2026",
  "hour12-1": "4",
  "hour12-2": "04",
  "hour24-1": "4",
  "hour24-2": "04",
  "minute-1": "5",
  "minute-2": "05",
  "second-1": "7",
  "second-2": "07",
  "period-1": "A",
  "period-2": "AM",
  "fraction-1": "1",
  "fraction-2": "12",
  "fraction-3": "123",
  "offset-1": "-4",
  "offset-2": "-04",
  "offset-3": "-04:00",
  "single-quote": "literal 2026",
  "double-quote": "double literal 2026",
  "escaped": "2026 y",
  "composite": "Thursday, August 6, 2026 at 4:05:07.123 AM -04:00"
};

export function loadPlaywright() {
  try {
    return require("playwright");
  } catch {
    return require(path.join(repoRoot, "Heimdall.Web", "node_modules", "playwright"));
  }
}

export async function getFreePort() {
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

export function run(command, args, options = {}) {
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

export function contentTypeFor(filePath) {
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

export function resolveStaticRequestPath(root, pathBase, requestPath) {
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

export async function withStaticFileServer(root, pathBase, callback) {
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

export async function waitForHttpOk(url, timeoutMs = 30000) {
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

export async function startApp() {
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

export function stopApp(child) {
  if (!child || child.killed)
    return;

  try {
    child.kill();
  } catch {
    // ignore
  }
}

export async function waitForText(locator, expected, timeoutMs = 5000) {
  const started = Date.now();
  while (Date.now() - started < timeoutMs) {
    const text = ((await locator.textContent().catch(() => "")) || "").trim();
    if (text === expected)
      return;

    await new Promise(resolve => setTimeout(resolve, 50));
  }

  assert.equal(((await locator.textContent().catch(() => "")) || "").trim(), expected);
}

export function waitForActionAfter(page, actionId, timeoutMs = 8000) {
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

export async function openHarness(page, baseUrl, pathName = "/") {
  await page.goto(`${baseUrl}${pathName}`);
  await page.locator("#e2e-harness").waitFor();
}

export async function assertLocalTimeMatrix(page, idPrefix) {
  await page.locator(`#${idPrefix}-composite`).waitFor();
  const actual = await page.evaluate(({ prefix, suffixes }) => {
    return Object.fromEntries(suffixes.map(suffix => {
      const element = document.getElementById(`${prefix}-${suffix}`);
      return [suffix, element ? element.textContent : null];
    }));
  }, { prefix: idPrefix, suffixes: Object.keys(expectedLocalTimeFormats) });

  assert.deepEqual(actual, expectedLocalTimeFormats);
}
