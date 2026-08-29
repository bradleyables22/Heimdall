import assert from "node:assert/strict";
import { loadPlaywright, startApp } from "./helpers/e2e-support.mjs";
import { tests as static_site } from "./specs/static-site.spec.mjs";
import { tests as time } from "./specs/time.spec.mjs";
import { tests as actions_and_swaps } from "./specs/actions-and-swaps.spec.mjs";
import { tests as mutations } from "./specs/mutations.spec.mjs";
import { tests as payloads_and_uploads } from "./specs/payloads-and-uploads.spec.mjs";
import { tests as events } from "./specs/events.spec.mjs";
import { tests as request_sync } from "./specs/request-sync.spec.mjs";
import { tests as lifecycle } from "./specs/lifecycle.spec.mjs";
import { tests as sse } from "./specs/sse.spec.mjs";
import { tests as antiforgery } from "./specs/antiforgery.spec.mjs";
import { tests as client_info } from "./specs/client-info.spec.mjs";
import { tests as request_headers } from "./specs/request-headers.spec.mjs";
import { tests as auth_and_errors } from "./specs/auth-and-errors.spec.mjs";

const tests = [
  ...static_site,
  ...time,
  ...actions_and_swaps,
  ...mutations,
  ...payloads_and_uploads,
  ...events,
  ...request_sync,
  ...lifecycle,
  ...sse,
  ...antiforgery,
  ...client_info,
  ...request_headers,
  ...auth_and_errors
];

async function runTests() {
  const { chromium } = loadPlaywright();
  const app = await startApp();
  const browser = await chromium.launch({ headless: true });
  try {
    for (const [name, fn, options = {}] of tests) {
      const context = await browser.newContext(options.contextOptions || {});
      const page = await context.newPage();
      page.setDefaultTimeout(8000);
      const browserErrors = [];
      page.on("pageerror", error => browserErrors.push(error?.message || String(error)));
      page.on("console", message => {
        if (message.type() === "error") browserErrors.push(message.text());
      });
      try {
        await fn(page, app.baseUrl);
        const allowed = options.allowedBrowserErrors || [];
        const unexpectedErrors = browserErrors.filter(error => !allowed.some(pattern => pattern.test(error)));
        assert.deepEqual(unexpectedErrors, [], "Browser should not emit unexpected console or page errors.");
        console.log(`ok - ${name}`);
      } catch (error) {
        console.error(`not ok - ${name}`);
        console.error(error?.stack || error);
        console.error(`
App output:
${app.output.slice(-30).join("")}`);
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

await runTests();
