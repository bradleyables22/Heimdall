import assert from "node:assert/strict";
import {
  actionFetches,
  csrfFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";

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

async function testEmptyOuterSwap(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: "" }]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<main id="host"><div id="remove">Remove me</div></main>';
    const result = await window.Heimdall.invoke("Swap.Remove", {}, { target: "#remove", swap: "outer" });

    return {
      ok: result.ok,
      html: result.html,
      targetExists: !!document.querySelector("#remove"),
      hostHtml: document.querySelector("#host").innerHTML
    };
  });

  assert.equal(state.ok, true);
  assert.equal(state.html, "");
  assert.equal(state.targetExists, false);
  assert.equal(state.hostHtml, "");
}

async function testScriptStripping(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: '<script>window.__heimdallScriptRan = true;</script><span id="clean">Clean</span>'
    }]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target">Old</div>';
    await window.Heimdall.invoke("Security.StripScripts", {}, { target: "#target" });

    return {
      html: document.querySelector("#target").innerHTML,
      ran: window.__heimdallScriptRan === true,
      scriptCount: document.querySelectorAll("#target script").length
    };
  });

  assert.equal(state.html, '<span id="clean">Clean</span>');
  assert.equal(state.ran, false);
  assert.equal(state.scriptCount, 0);
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

async function testMissingOutOfBandTarget(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: `
        <invocation heimdall-content-target="#missing">
          <template><em id="missing-side">Missing</em></template>
        </invocation>
        <span id="main-done">Main</span>
      `
    }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = '<div id="main">Old main</div>';
    return window.Heimdall.invoke("Oob.MissingTarget", {}, { target: "#main" });
  });

  assert.equal(await page.locator("#main").innerHTML(), '\n        \n        <span id="main-done">Main</span>\n      ');
  assert.equal(await page.locator("invocation").count(), 0);
  assert.equal(await page.locator("#missing-side").count(), 0);
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

async function testRedirectTextDirective(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<redirect>#text-redirected</redirect><span>Ignored</span>' }]
  });

  const result = await page.evaluate(() => {
    document.body.innerHTML = '<div id="main">Keep</div>';
    return window.Heimdall.invoke("Redirect.Text", {}, { target: "#main" });
  });

  assert.equal(result.redirectUrl, "#text-redirected");
  assert.equal(result.abortReason, "redirect");
  assert.equal(await page.locator("#main").innerHTML(), "Keep");
  assert.equal(new URL(page.url()).hash, "#text-redirected");
}

async function testFetchFollowedAuthRedirect(page) {
  const serverSignInUrl = "http://heimdall.test/signin?ReturnUrl=%2F__heimdall%2Fv1%2Fcontent%2Factions";
  const expectedSignInUrl = "http://heimdall.test/signin?ReturnUrl=%2F";

  await installFakeServer(page, {
    actionResponses: [{
      body: '<form id="login-form">Sign in</form>',
      redirected: true,
      url: serverSignInUrl
    }]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="main">Secure content</div>';
    const result = await window.Heimdall.invoke("Secure.Refresh", {}, { target: "#main" });

    return {
      result: {
        ok: result.ok,
        status: result.status,
        redirectUrl: result.redirectUrl,
        abortSwap: result.abortSwap,
        abortReason: result.abortReason
      },
      targetHtml: document.querySelector("#main").innerHTML
    };
  });

  await page.waitForURL(expectedSignInUrl);

  assert.deepEqual(state.result, {
    ok: true,
    status: 200,
    redirectUrl: expectedSignInUrl,
    abortSwap: true,
    abortReason: "redirect"
  });
  assert.equal(state.targetHtml, "Secure content");
  assert.equal(page.url(), expectedSignInUrl);
}

async function testAuthReturnUrlParameterCaseInsensitive(page) {
  const serverSignInUrl = "http://heimdall.test/signin?returnUrl=%2F__heimdall%2Fv1%2Fcontent%2Factions";
  const expectedSignInUrl = "http://heimdall.test/signin?returnUrl=%2F";

  await installFakeServer(page, {
    actionResponses: [{
      body: '<form id="login-form">Sign in</form>',
      redirected: true,
      url: serverSignInUrl
    }]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="main">Secure content</div>';
    const result = await window.Heimdall.invoke("Secure.Refresh", {}, { target: "#main" });

    return {
      redirectUrl: result.redirectUrl,
      targetHtml: document.querySelector("#main").innerHTML
    };
  });

  await page.waitForURL(expectedSignInUrl);

  assert.equal(state.redirectUrl, expectedSignInUrl);
  assert.equal(state.targetHtml, "Secure content");
  assert.equal(page.url(), expectedSignInUrl);
}

async function testOobDisabled(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: `
        <invocation heimdall-content-target="#side">
          <template><em id="side-should-not-change">Side</em></template>
        </invocation>
        <span id="main-done">Main</span>
      `
    }]
  });

  await page.evaluate(() => {
    window.Heimdall.config.oobEnabled = false;
    document.body.innerHTML = '<div id="main">Old main</div><div id="side">Old side</div>';
    return window.Heimdall.invoke("Oob.Disabled", {}, { target: "#main" });
  });

  assert.equal(await page.locator("#main #main-done").count(), 1);
  assert.equal(await page.locator("#main invocation").count(), 0);
  assert.equal(await page.locator("#side").innerHTML(), "Old side");
  assert.equal(await page.locator("#side-should-not-change").count(), 0);
}

async function testCsrfRetry(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-1", "csrf-2"],
    actionResponses: [
      { status: 400, body: "csrf validation failed" },
      { status: 200, body: '<span id="retry-ok">Retried</span>' }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target">Old</div>';
    const attempts = [];
    let afterCount = 0;
    let finallyCount = 0;

    document.addEventListener("heimdall:request-before", event => {
      if (event.detail.actionId === "Retry.Save")
        attempts.push(event.detail.attempt);
    });
    document.addEventListener("heimdall:request-after", event => {
      if (event.detail.actionId === "Retry.Save")
        afterCount++;
    });
    document.addEventListener("heimdall:request-finally", event => {
      if (event.detail.actionId === "Retry.Save")
        finallyCount++;
    });

    const result = await window.Heimdall.invoke("Retry.Save", {}, { target: "#target" });
    return { result, attempts, afterCount, finallyCount };
  });

  assert.equal(state.result.ok, true);
  assert.deepEqual(state.attempts, [1, 2]);
  assert.equal(state.afterCount, 1);
  assert.equal(state.finallyCount, 1);
  assert.equal(await page.locator("#target").innerHTML(), '<span id="retry-ok">Retried</span>');

  const fetches = await getFetches(page);
  const actions = actionFetches(fetches);

  assert.equal(csrfFetches(fetches).length, 2);
  assert.equal(actions.length, 2);
  assert.equal(actions[0].headers.requestverificationtoken, "csrf-1");
  assert.equal(actions[1].headers.requestverificationtoken, "csrf-2");
}

export const tests = [
  ["applies all swap modes", testSwapModes],
  ["removes outer targets on empty outer swaps", testEmptyOuterSwap],
  ["strips script elements from swapped HTML", testScriptStripping],
  ["processes out-of-band invocations", testOutOfBandInvocation],
  ["strips out-of-band invocations with missing targets", testMissingOutOfBandTarget],
  ["honors abort directives while keeping OOB updates", testAbortDirective],
  ["honors redirect directives", testRedirectDirective],
  ["honors redirect text content", testRedirectTextDirective],
  ["navigates on fetch-followed auth redirects", testFetchFollowedAuthRedirect],
  ["rewrites auth return URL params case-insensitively", testAuthReturnUrlParameterCaseInsensitive],
  ["strips OOB invocations when OOB is disabled", testOobDisabled],
  ["retries once after suspected CSRF failure", testCsrfRetry]
];
