import assert from "node:assert/strict";
import {
  actionFetches,
  csrfFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";

async function testDefaultParallelRequests(page) {
  await installFakeServer(page, {
    actionResponses: [
      { body: '<span id="parallel-first">First</span>', delayMs: 50 },
      { body: '<span id="parallel-second">Second</span>', delayMs: 5 }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target">Old</div>';

    const first = window.Heimdall.invoke("Parallel.First", {}, { target: "#target" });
    const second = window.Heimdall.invoke("Parallel.Second", {}, { target: "#target" });
    const results = await Promise.all([first, second]);

    return {
      results: results.map(result => ({ ok: result.ok, cancelled: !!result.cancelled })),
      targetHtml: document.querySelector("#target").innerHTML
    };
  });

  assert.deepEqual(state.results, [
    { ok: true, cancelled: false },
    { ok: true, cancelled: false }
  ]);
  assert.equal(state.targetHtml, '<span id="parallel-first">First</span>');
  assert.equal(actionFetches(await getFetches(page)).length, 2);
}

async function testRequestSyncReplace(page) {
  await installFakeServer(page, {
    actionResponses: [
      {
        body: '<invocation heimdall-content-target="#side"><template><b id="stale-side">Stale side</b></template></invocation><javascript function="window.App.stale"></javascript><span id="stale">Stale</span>',
        delayMs: 60,
        ignoreAbort: true
      },
      { body: '<span id="fresh">Fresh</span>', delayMs: 5 }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target">Old</div><div id="side">Keep side</div>';
    window.App = {
      stale() {
        window.__staleJsRan = true;
      }
    };
    const cancellations = [];
    document.addEventListener("heimdall:request-cancel", event => {
      cancellations.push({
        actionId: event.detail.actionId,
        reason: event.detail.result.cancelReason
      });
    });

    let resolveStarted;
    const started = new Promise(resolve => { resolveStarted = resolve; });
    document.addEventListener("heimdall:request-before", event => {
      if (event.detail.actionId === "Replace.Stale")
        resolveStarted();
    });

    const stale = window.Heimdall.invoke("Replace.Stale", {}, {
      target: "#target",
      sync: "replace",
      syncGroup: "search"
    });
    await started;

    const fresh = window.Heimdall.invoke("Replace.Fresh", {}, {
      target: "#target",
      sync: "replace",
      syncGroup: "search"
    });

    const [staleResult, freshResult] = await Promise.all([stale, fresh]);
    return {
      staleResult: {
        ok: staleResult.ok,
        cancelled: staleResult.cancelled,
        cancelReason: staleResult.cancelReason
      },
      freshResult: { ok: freshResult.ok, cancelled: !!freshResult.cancelled },
      cancellations,
      targetHtml: document.querySelector("#target").innerHTML,
      sideHtml: document.querySelector("#side").innerHTML,
      staleJsRan: window.__staleJsRan === true
    };
  });

  assert.deepEqual(state.staleResult, {
    ok: false,
    cancelled: true,
    cancelReason: "replaced"
  });
  assert.deepEqual(state.freshResult, { ok: true, cancelled: false });
  assert.deepEqual(state.cancellations, [{ actionId: "Replace.Stale", reason: "replaced" }]);
  assert.equal(state.targetHtml, '<span id="fresh">Fresh</span>');
  assert.equal(state.sideHtml, "Keep side");
  assert.equal(state.staleJsRan, false);

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 2);
  assert.equal(actions[0].aborted, true);
}

async function testRequestSyncDrop(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="drop-first">First</span>', delayMs: 35 }]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target">Old</div>';

    let resolveStarted;
    const started = new Promise(resolve => { resolveStarted = resolve; });
    document.addEventListener("heimdall:request-before", event => {
      if (event.detail.actionId === "Drop.First")
        resolveStarted();
    });

    const first = window.Heimdall.invoke("Drop.First", {}, {
      target: "#target",
      sync: "drop",
      syncGroup: "save"
    });
    await started;

    const dropped = window.Heimdall.invoke("Drop.Second", {}, {
      target: "#target",
      sync: "drop",
      syncGroup: "save"
    });

    const [firstResult, droppedResult] = await Promise.all([first, dropped]);
    return {
      firstOk: firstResult.ok,
      dropped: {
        cancelled: droppedResult.cancelled,
        cancelReason: droppedResult.cancelReason
      },
      targetHtml: document.querySelector("#target").innerHTML
    };
  });

  assert.equal(state.firstOk, true);
  assert.deepEqual(state.dropped, { cancelled: true, cancelReason: "dropped" });
  assert.equal(state.targetHtml, '<span id="drop-first">First</span>');
  assert.equal(actionFetches(await getFetches(page)).length, 1);
}

async function testRequestSyncQueueLatest(page) {
  await installFakeServer(page, {
    actionResponses: [
      { body: '<span id="queue-first">First</span>', delayMs: 35 },
      { body: '<span id="queue-third">Third</span>', delayMs: 5 }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target">Old</div>';

    let resolveStarted;
    const started = new Promise(resolve => { resolveStarted = resolve; });
    document.addEventListener("heimdall:request-before", event => {
      if (event.detail.actionId === "Queue.First")
        resolveStarted();
    });

    const options = { target: "#target", sync: "queue-latest", syncGroup: "queue" };
    const first = window.Heimdall.invoke("Queue.First", { order: 1 }, options);
    await started;
    const second = window.Heimdall.invoke("Queue.Second", { order: 2 }, options);
    const third = window.Heimdall.invoke("Queue.Third", { order: 3 }, options);

    const [firstResult, secondResult, thirdResult] = await Promise.all([first, second, third]);
    return {
      firstOk: firstResult.ok,
      second: {
        cancelled: secondResult.cancelled,
        cancelReason: secondResult.cancelReason
      },
      thirdOk: thirdResult.ok,
      targetHtml: document.querySelector("#target").innerHTML
    };
  });

  assert.equal(state.firstOk, true);
  assert.deepEqual(state.second, { cancelled: true, cancelReason: "queue-replaced" });
  assert.equal(state.thirdOk, true);
  assert.equal(state.targetHtml, '<span id="queue-third">Third</span>');

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 2);
  assert.deepEqual(actions.map(action => action.jsonBody.order), [1, 3]);
}

async function testDeclarativeSyncGroup(page) {
  await installFakeServer(page, {
    actionResponses: [
      { body: '<span id="declarative-stale">Stale</span>', delayMs: 60 },
      { body: '<span id="declarative-fresh">Fresh</span>', delayMs: 5 }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <button id="first"
              heimdall-content-click="Declarative.First"
              heimdall-content-target="#target"
              heimdall-content-disable="false"
              heimdall-sync="replace"
              heimdall-sync-group="shared">First</button>
      <button id="second"
              heimdall-content-click="Declarative.Second"
              heimdall-content-target="#target"
              heimdall-content-disable="false"
              heimdall-sync="replace"
              heimdall-sync-group="shared">Second</button>
      <div id="target">Old</div>
    `;

    let resolveFirstStarted;
    const firstStarted = new Promise(resolve => { resolveFirstStarted = resolve; });
    const completions = [];
    let resolveCompleted;
    const completed = new Promise(resolve => { resolveCompleted = resolve; });

    document.addEventListener("heimdall:request-before", event => {
      if (event.detail.actionId === "Declarative.First")
        resolveFirstStarted();
    });
    document.addEventListener("heimdall:request-finally", event => {
      if (event.detail.actionId.startsWith("Declarative.")) {
        completions.push(event.detail.actionId);
        if (completions.length === 2)
          resolveCompleted();
      }
    });

    document.querySelector("#first").click();
    await firstStarted;
    document.querySelector("#second").click();
    await completed;

    return {
      completions,
      targetHtml: document.querySelector("#target").innerHTML
    };
  });

  assert.equal(state.completions.length, 2);
  assert.equal(state.targetHtml, '<span id="declarative-fresh">Fresh</span>');
  assert.equal(actionFetches(await getFetches(page)).length, 2);
}

async function testDeclarativeElementSync(page) {
  await installFakeServer(page, {
    actionResponses: [
      { body: '<span id="element-stale">Stale</span>', delayMs: 60 },
      { body: '<span id="element-fresh">Fresh</span>', delayMs: 5 }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <button id="refresh"
              heimdall-content-click="Element.Refresh"
              heimdall-content-target="#target"
              heimdall-content-disable="false"
              heimdall-sync="replace">Refresh</button>
      <div id="target">Old</div>
    `;

    let beforeCount = 0;
    let finallyCount = 0;
    let resolveFirstStarted;
    let resolveCompleted;
    const firstStarted = new Promise(resolve => { resolveFirstStarted = resolve; });
    const completed = new Promise(resolve => { resolveCompleted = resolve; });

    document.addEventListener("heimdall:request-before", event => {
      if (event.detail.actionId === "Element.Refresh" && ++beforeCount === 1)
        resolveFirstStarted();
    });
    document.addEventListener("heimdall:request-finally", event => {
      if (event.detail.actionId === "Element.Refresh" && ++finallyCount === 2)
        resolveCompleted();
    });

    const button = document.querySelector("#refresh");
    button.click();
    await firstStarted;
    button.click();
    await completed;

    return {
      beforeCount,
      finallyCount,
      targetHtml: document.querySelector("#target").innerHTML
    };
  });

  assert.equal(state.beforeCount, 2);
  assert.equal(state.finallyCount, 2);
  assert.equal(state.targetHtml, '<span id="element-fresh">Fresh</span>');
  assert.equal(actionFetches(await getFetches(page)).length, 2);
}

async function testReplacementBusyState(page) {
  await installFakeServer(page, {
    actionResponses: [
      { body: '<span>Old</span>', delayMs: 200 },
      { body: '<span>New</span>', delayMs: 50 }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<button id="source">Save</button><div id="target">Old</div>';
    const source = document.querySelector("#source");

    let resolveStarted;
    const started = new Promise(resolve => { resolveStarted = resolve; });
    document.addEventListener("heimdall:request-before", event => {
      if (event.detail.actionId === "Busy.First")
        resolveStarted();
    });

    const common = {
      target: "#target",
      sourceEl: source,
      disableElement: source,
      sync: "replace",
      syncGroup: "busy"
    };

    const first = window.Heimdall.invoke("Busy.First", {}, common);
    await started;
    const second = window.Heimdall.invoke("Busy.Second", {}, common);

    const firstResult = await first;
    const duringSecond = {
      disabled: source.hasAttribute("disabled"),
      busy: source.getAttribute("aria-busy")
    };
    const secondResult = await second;

    return {
      firstCancelled: firstResult.cancelled,
      secondOk: secondResult.ok,
      duringSecond,
      after: {
        disabled: source.hasAttribute("disabled"),
        busy: source.getAttribute("aria-busy")
      }
    };
  });

  assert.equal(state.firstCancelled, true);
  assert.equal(state.secondOk, true);
  assert.deepEqual(state.duringSecond, { disabled: true, busy: "true" });
  assert.deepEqual(state.after, { disabled: false, busy: null });
}

export const tests = [
  ["keeps parallel request behavior by default", testDefaultParallelRequests],
  ["replaces active requests and rejects stale responses", testRequestSyncReplace],
  ["drops incoming requests while a group is active", testRequestSyncDrop],
  ["queues only the latest pending request", testRequestSyncQueueLatest],
  ["coordinates declarative triggers through sync groups", testDeclarativeSyncGroup],
  ["scopes declarative synchronization to an element by default", testDeclarativeElementSync],
  ["keeps busy state until replacement requests finish", testReplacementBusyState]
];
