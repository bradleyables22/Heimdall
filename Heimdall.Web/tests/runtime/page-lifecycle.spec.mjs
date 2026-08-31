import assert from "node:assert/strict";
import {
  actionFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";

async function waitForActionCount(page, count) {
  await page.waitForFunction(expected => {
    const fetches = window.__heimdallFetches || [];
    return fetches.filter(fetch => fetch.url.includes("/__heimdall/v1/content/actions")).length === expected;
  }, count);
}

async function testOnlineInvokesEveryCurrentMatchingElement(page) {
  await installFakeServer(page);

  await page.evaluate(() => {
    document.body.innerHTML = `
      <div id="first"
           heimdall-content-online="Connection.First"
           heimdall-content-swap="none"></div>
      <div id="second"
           heimdall-content-online="Connection.Second"
           heimdall-content-swap="none"></div>
      <div heimdall-content-online="   " heimdall-content-swap="none"></div>
      <div heimdall-content-online="Connection.Disabled"
           heimdall-content-swap="none"
           disabled></div>
    `;
  });

  assert.equal(actionFetches(await getFetches(page)).length, 0, "Online must not fire during boot.");

  await page.evaluate(() => window.dispatchEvent(new Event("online")));
  await waitForActionCount(page, 2);

  await page.evaluate(() => {
    document.body.insertAdjacentHTML(
      "beforeend",
      '<div id="dynamic" heimdall-content-online="Connection.Dynamic" heimdall-content-swap="none"></div>'
    );
    window.dispatchEvent(new Event("online"));
  });
  await waitForActionCount(page, 5);

  const actions = actionFetches(await getFetches(page));
  assert.deepEqual(
    actions.map(action => action.headers["x-heimdall-content-action"]),
    [
      "Connection.First",
      "Connection.Second",
      "Connection.First",
      "Connection.Second",
      "Connection.Dynamic"
    ]
  );
}

async function testDocumentVisibleOnlyInvokesWhileVisible(page) {
  await installFakeServer(page);

  await page.evaluate(() => {
    document.body.innerHTML = `
      <div id="visible-first"
           heimdall-content-document-visible="Dashboard.First"
           heimdall-content-swap="none"></div>
    `;

    window.__heimdallTestVisibility = "hidden";
    Object.defineProperty(document, "visibilityState", {
      configurable: true,
      get: () => window.__heimdallTestVisibility
    });
  });

  assert.equal(actionFetches(await getFetches(page)).length, 0, "DocumentVisible must not fire during boot.");

  await page.evaluate(() => document.dispatchEvent(new Event("visibilitychange")));
  await page.waitForTimeout(50);
  assert.equal(actionFetches(await getFetches(page)).length, 0, "Hidden transitions must not invoke actions.");

  await page.evaluate(() => {
    window.__heimdallTestVisibility = "visible";
    document.dispatchEvent(new Event("visibilitychange"));
  });
  await waitForActionCount(page, 1);

  await page.evaluate(() => {
    window.__heimdallTestVisibility = "hidden";
    document.dispatchEvent(new Event("visibilitychange"));
    document.body.insertAdjacentHTML(
      "beforeend",
      '<div id="visible-dynamic" heimdall-content-document-visible="Dashboard.Dynamic" heimdall-content-swap="none"></div>'
    );
  });
  await page.waitForTimeout(50);
  assert.equal(actionFetches(await getFetches(page)).length, 1);

  await page.evaluate(() => {
    window.__heimdallTestVisibility = "visible";
    document.dispatchEvent(new Event("visibilitychange"));
  });
  await waitForActionCount(page, 3);

  const actions = actionFetches(await getFetches(page));
  assert.deepEqual(
    actions.map(action => action.headers["x-heimdall-content-action"]),
    ["Dashboard.First", "Dashboard.First", "Dashboard.Dynamic"]
  );
}

async function testLifecycleTriggersUseNormalPayloadAndSwapPipeline(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<strong id="online-result">Restored</strong>' }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <div id="online-source"
           heimdall-content-online="Connection.Restore"
           heimdall-payload='{"reason":"browser-online"}'
           heimdall-content-target="#online-target"
           heimdall-content-swap="inner"></div>
      <div id="online-target">Waiting</div>
    `;
    window.dispatchEvent(new Event("online"));
  });

  await page.waitForSelector("#online-result");
  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 1);
  assert.equal(actions[0].headers["x-heimdall-content-action"], "Connection.Restore");
  assert.deepEqual(actions[0].jsonBody, { reason: "browser-online" });
  assert.equal(await page.locator("#online-target").innerText(), "Restored");
}

async function testOfflineEmitsClientEventWithoutRequest(page) {
  await installFakeServer(page);

  const result = await page.evaluate(async () => {
    document.body.innerHTML = `
      <div heimdall-content-online="Connection.Restore" heimdall-content-swap="none"></div>
      <div heimdall-content-document-visible="Dashboard.Refresh" heimdall-content-swap="none"></div>
    `;

    const events = [];
    document.addEventListener("heimdall:offline", event => {
      events.push({
        targetIsDocument: event.target === document,
        bubbles: event.bubbles,
        cancelable: event.cancelable,
        online: event.detail?.online
      });
    });

    window.dispatchEvent(new Event("offline"));
    window.dispatchEvent(new Event("offline"));
    await Promise.resolve();
    return events;
  });

  assert.deepEqual(result, [
    { targetIsDocument: true, bubbles: false, cancelable: false, online: false },
    { targetIsDocument: true, bubbles: false, cancelable: false, online: false }
  ]);
  assert.equal(actionFetches(await getFetches(page)).length, 0, "Offline must never invoke or queue an action.");
}

export const tests = [
  ["invokes every current online trigger on each online event", testOnlineInvokesEveryCurrentMatchingElement],
  ["invokes document-visible triggers only when the document is visible", testDocumentVisibleOnlyInvokesWhileVisible],
  ["runs lifecycle triggers through the normal payload and swap pipeline", testLifecycleTriggersUseNormalPayloadAndSwapPipeline],
  ["emits a client-only offline event without making a request", testOfflineEmitsClientEventWithoutRequest]
];
