import path from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright";
import { createRuntimePage } from "./helpers/runtime-page.mjs";
import { tests as timeFormatting } from "./runtime/time-formatting.spec.mjs";
import { tests as timeDomIntegration } from "./runtime/time-dom-integration.spec.mjs";
import { tests as actionsApiLifecycle } from "./runtime/actions-api-lifecycle.spec.mjs";
import { tests as requestCancellation } from "./runtime/request-cancellation.spec.mjs";
import { tests as responseBehavior } from "./runtime/response-behavior.spec.mjs";
import { tests as requestSync } from "./runtime/request-sync.spec.mjs";
import { tests as queuedStateRefresh } from "./runtime/queued-state-refresh.spec.mjs";
import { tests as queuedStateSnapshots } from "./runtime/queued-state-snapshots.spec.mjs";
import { tests as queuedStateRebinding } from "./runtime/queued-state-rebinding.spec.mjs";
import { tests as queuedTargets } from "./runtime/queued-targets.spec.mjs";
import { tests as events } from "./runtime/events.spec.mjs";
import { tests as payloads } from "./runtime/payloads.spec.mjs";
import { tests as swaps } from "./runtime/swaps.spec.mjs";
import { tests as mutations } from "./runtime/mutations.spec.mjs";
import { tests as javascript } from "./runtime/javascript.spec.mjs";
import { tests as sseSecurity } from "./runtime/sse-security.spec.mjs";
import { tests as antiforgeryConfig } from "./runtime/antiforgery-config.spec.mjs";
import { tests as clientInfo } from "./runtime/client-info.spec.mjs";
import { tests as requestHeaders } from "./runtime/request-headers.spec.mjs";
import { tests as sseDelivery } from "./runtime/sse-delivery.spec.mjs";
import { tests as sseReconnect } from "./runtime/sse-reconnect.spec.mjs";
import { tests as sseLifecycle } from "./runtime/sse-lifecycle.spec.mjs";

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const runtimes = [
  { name: "bundle", path: path.join(projectRoot, "wwwroot", "heimdall-bundle.js") },
  { name: "minified bundle", path: path.join(projectRoot, "wwwroot", "heimdall-bundle.min.js") }
];
const tests = [
  ...timeFormatting,
  ...timeDomIntegration,
  ...actionsApiLifecycle,
  ...requestCancellation,
  ...responseBehavior,
  ...requestSync,
  ...queuedStateRefresh,
  ...queuedStateSnapshots,
  ...queuedStateRebinding,
  ...queuedTargets,
  ...events,
  ...payloads,
  ...swaps,
  ...mutations,
  ...javascript,
  ...sseSecurity,
  ...antiforgeryConfig,
  ...clientInfo,
  ...requestHeaders,
  ...sseDelivery,
  ...sseReconnect,
  ...sseLifecycle
];

function withTimeout(promise, label, ms = 8000) {
  let timer;
  const timeout = new Promise((_, reject) => {
    timer = setTimeout(() => reject(new Error(`Timed out after ${ms}ms: ${label}`)), ms);
  });
  return Promise.race([promise, timeout]).finally(() => clearTimeout(timer));
}

let failures = 0;
const browser = await chromium.launch();
try {
  for (const runtime of runtimes) {
    for (const [name, test, testOptions = {}] of tests) {
      const label = `${runtime.name}: ${name}`;
      let page = null;
      try {
        page = await withTimeout(createRuntimePage(browser, runtime, testOptions.runtimePage || {}), `${label} setup`);
        await withTimeout(test(page), label);
        console.log(`ok - ${label}`);
      } catch (error) {
        failures += 1;
        console.error(`not ok - ${label}`);
        console.error(error);
      } finally {
        if (page) await page.close();
      }
    }
  }
} finally {
  await browser.close();
}

if (failures > 0) throw new Error(`${failures} Heimdall runtime test(s) failed.`);
console.log(`Heimdall runtime tests passed (${runtimes.length * tests.length} checks).`);
