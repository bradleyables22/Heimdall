import assert from "node:assert/strict";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright";
import {
  actionFetches,
  createRuntimePage,
  csrfFetches,
  getFetches,
  installFakeServer
} from "./helpers/runtime-page.mjs";

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const runtimes = [
  {
    name: "reference",
    path: path.join(projectRoot, "wwwroot", "heimdall.js")
  },
  {
    name: "bundle",
    path: path.join(projectRoot, "wwwroot", "heimdall-bundle.js")
  }
];

const tests = [
  ["exposes the public API", testPublicApi],
  ["invokes actions with CSRF and swaps HTML", testProgrammaticInvoke],
  ["handles delegated click triggers", testClickTrigger],
  ["serializes submit payloads from forms", testSubmitPayload],
  ["resolves closest state payloads", testClosestStatePayload],
  ["applies all swap modes", testSwapModes],
  ["processes out-of-band invocations", testOutOfBandInvocation],
  ["honors abort directives while keeping OOB updates", testAbortDirective],
  ["honors redirect directives", testRedirectDirective],
  ["retries once after suspected CSRF failure", testCsrfRetry],
  ["boots inserted load triggers with MutationObserver", testMutationObserverBoot]
];

function withTimeout(promise, label, ms = 8000) {
  let timer;
  const timeout = new Promise((_, reject) => {
    timer = setTimeout(() => reject(new Error(`Timed out after ${ms}ms: ${label}`)), ms);
  });

  return Promise.race([promise, timeout]).finally(() => clearTimeout(timer));
}

async function testPublicApi(page) {
  const api = await page.evaluate(() => ({
    apiVersion: window.Heimdall.apiVersion,
    invoke: typeof window.Heimdall.invoke,
    boot: typeof window.Heimdall.boot,
    onReady: typeof window.Heimdall.onReady,
    clearCsrfToken: typeof window.Heimdall.clearCsrfToken,
    sseConnect: typeof window.Heimdall.sse.connect,
    contentEndpoint: window.Heimdall.config.endpoints.contentActions
  }));

  assert.equal(api.apiVersion, 1);
  assert.equal(api.invoke, "function");
  assert.equal(api.boot, "function");
  assert.equal(api.onReady, "function");
  assert.equal(api.clearCsrfToken, "function");
  assert.equal(api.sseConnect, "function");
  assert.equal(api.contentEndpoint, "/__heimdall/v1/content/actions");
}

async function testProgrammaticInvoke(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="updated">Updated</span>' }]
  });

  const result = await page.evaluate(() => {
    document.body.innerHTML = '<div id="target">Old</div>';
    return window.Heimdall.invoke("Notes.Save", { id: 42 }, { target: "#target" });
  });

  assert.equal(result.ok, true);
  assert.equal(await page.locator("#target").innerHTML(), '<span id="updated">Updated</span>');

  const fetches = await getFetches(page);
  const actions = actionFetches(fetches);

  assert.equal(csrfFetches(fetches).length, 1);
  assert.equal(actions.length, 1);
  assert.equal(actions[0].method, "POST");
  assert.equal(new URL(actions[0].url).searchParams.get("action"), "Notes.Save");
  assert.equal(actions[0].headers["x-heimdall-content-action"], "Notes.Save");
  assert.equal(actions[0].headers.requestverificationtoken, "csrf-token");
  assert.deepEqual(actions[0].jsonBody, { id: 42 });
}

async function testClickTrigger(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<strong id="clicked">Clicked</strong>' }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <button id="save"
              heimdall-content-click="Buttons.Save"
              heimdall-content-target="#target">
        Save
      </button>
      <div id="target">Old</div>
    `;
  });

  await page.click("#save");
  await page.waitForSelector("#clicked");

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 1);
  assert.equal(actions[0].headers["x-heimdall-content-action"], "Buttons.Save");
}

async function testSubmitPayload(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="saved">Saved</span>' }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <form id="form" heimdall-content-submit="Forms.Save" heimdall-content-target="#target">
        <input name="title" value="Hello">
        <input type="checkbox" name="tag" value="a" checked>
        <input type="checkbox" name="tag" value="b" checked>
        <button type="submit">Submit</button>
      </form>
      <div id="target">Old</div>
    `;
  });

  await page.locator("#form").evaluate(form => form.requestSubmit());
  await page.waitForSelector("#saved");

  const actions = actionFetches(await getFetches(page));
  assert.deepEqual(actions[0].jsonBody, { title: "Hello", tag: ["a", "b"] });
}

async function testClosestStatePayload(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="filtered">Filtered</span>' }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <div data-heimdall-state='{"filter":"open","page":2}'>
        <button id="filter"
                heimdall-content-click="Filters.Apply"
                heimdall-payload-from="closest-state"
                heimdall-content-target="#target">
          Filter
        </button>
      </div>
      <div id="target">Old</div>
    `;
  });

  await page.click("#filter");
  await page.waitForSelector("#filtered");

  const actions = actionFetches(await getFetches(page));
  assert.deepEqual(actions[0].jsonBody, { filter: "open", page: 2 });
}

async function testSwapModes(page) {
  await installFakeServer(page, {
    actionResponses: [
      { body: "<span>A</span>" },
      { body: "<span>B</span>" },
      { body: "<span>C</span>" },
      { body: '<section id="outer">D</section>' },
      { body: "<span>E</span>" }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target">M</div><div id="outer">Old</div><div id="none">Keep</div>';

    await window.Heimdall.invoke("Swap.Inner", {}, { target: "#target", swap: "inner" });
    const afterInner = document.querySelector("#target").innerHTML;

    await window.Heimdall.invoke("Swap.BeforeEnd", {}, { target: "#target", swap: "beforeend" });
    const afterBeforeEnd = document.querySelector("#target").innerHTML;

    await window.Heimdall.invoke("Swap.AfterBegin", {}, { target: "#target", swap: "afterbegin" });
    const afterAfterBegin = document.querySelector("#target").innerHTML;

    await window.Heimdall.invoke("Swap.Outer", {}, { target: "#outer", swap: "outer" });
    const afterOuter = document.querySelector("#outer").outerHTML;

    await window.Heimdall.invoke("Swap.None", {}, { target: "#none", swap: "none" });
    const afterNone = document.querySelector("#none").innerHTML;

    return { afterInner, afterBeforeEnd, afterAfterBegin, afterOuter, afterNone };
  });

  assert.equal(state.afterInner, "<span>A</span>");
  assert.equal(state.afterBeforeEnd, "<span>A</span><span>B</span>");
  assert.equal(state.afterAfterBegin, "<span>C</span><span>A</span><span>B</span>");
  assert.equal(state.afterOuter, '<section id="outer">D</section>');
  assert.equal(state.afterNone, "Keep");
}

async function testOutOfBandInvocation(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: `
        <invocation heimdall-content-target="#side">
          <template><em id="side-done">Side</em></template>
        </invocation>
        <span id="main-done">Main</span>
      `
    }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = '<div id="main">Old main</div><div id="side">Old side</div>';
    return window.Heimdall.invoke("Oob.Update", {}, { target: "#main" });
  });

  assert.equal(await page.locator("#main").innerHTML(), '\n        \n        <span id="main-done">Main</span>\n      ');
  assert.equal(await page.locator("#side").innerHTML(), '<em id="side-done">Side</em>');
}

async function testAbortDirective(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: `
        <abort reason="unchanged"></abort>
        <invocation heimdall-content-target="#side">
          <template><strong id="side-updated">Side</strong></template>
        </invocation>
        <span id="should-not-apply">Nope</span>
      `
    }]
  });

  const result = await page.evaluate(() => {
    document.body.innerHTML = '<div id="main">Keep main</div><div id="side">Old side</div>';
    return window.Heimdall.invoke("Abort.Update", {}, { target: "#main" });
  });

  assert.equal(result.abortSwap, true);
  assert.equal(result.abortReason, "unchanged");
  assert.equal(await page.locator("#main").innerHTML(), "Keep main");
  assert.equal(await page.locator("#side").innerHTML(), '<strong id="side-updated">Side</strong>');
}

async function testRedirectDirective(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<redirect url="#redirected"></redirect><span>Ignored</span>' }]
  });

  const result = await page.evaluate(() => {
    document.body.innerHTML = '<div id="main">Keep</div>';
    return window.Heimdall.invoke("Redirect.Go", {}, { target: "#main" });
  });

  assert.equal(result.redirectUrl, "#redirected");
  assert.equal(result.abortSwap, true);
  assert.equal(await page.locator("#main").innerHTML(), "Keep");
  assert.equal(new URL(page.url()).hash, "#redirected");
}

async function testCsrfRetry(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-1", "csrf-2"],
    actionResponses: [
      { status: 400, body: "csrf validation failed" },
      { status: 200, body: '<span id="retry-ok">Retried</span>' }
    ]
  });

  const result = await page.evaluate(() => {
    document.body.innerHTML = '<div id="target">Old</div>';
    return window.Heimdall.invoke("Retry.Save", {}, { target: "#target" });
  });

  assert.equal(result.ok, true);
  assert.equal(await page.locator("#target").innerHTML(), '<span id="retry-ok">Retried</span>');

  const fetches = await getFetches(page);
  const actions = actionFetches(fetches);

  assert.equal(csrfFetches(fetches).length, 2);
  assert.equal(actions.length, 2);
  assert.equal(actions[0].headers.requestverificationtoken, "csrf-1");
  assert.equal(actions[1].headers.requestverificationtoken, "csrf-2");
}

async function testMutationObserverBoot(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="loaded">Loaded</span>' }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = '<main id="host"></main>';
    document.querySelector("#host").insertAdjacentHTML(
      "beforeend",
      '<div id="load-target" heimdall-content-load="Load.Dynamic"></div>'
    );
  });

  await page.waitForSelector("#loaded");

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 1);
  assert.equal(actions[0].headers["x-heimdall-content-action"], "Load.Dynamic");
}

let failures = 0;
const browser = await chromium.launch();

try {
  for (const runtime of runtimes) {
    for (const [name, test] of tests) {
      const label = `${runtime.name}: ${name}`;
      let page = null;

      try {
        page = await withTimeout(createRuntimePage(browser, runtime), `${label} setup`);
        await withTimeout(test(page), label);
        console.log(`ok - ${label}`);
      } catch (error) {
        failures += 1;
        console.error(`not ok - ${label}`);
        console.error(error);
      } finally {
        if (page) {
          await page.close();
        }
      }
    }
  }
} finally {
  await browser.close();
}

if (failures > 0) {
  throw new Error(`${failures} Heimdall runtime test(s) failed.`);
}

console.log(`Heimdall runtime parity tests passed (${runtimes.length * tests.length} checks).`);
