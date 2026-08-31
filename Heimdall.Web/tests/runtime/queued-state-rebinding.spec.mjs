import assert from "node:assert/strict";
import {
  actionFetches,
  csrfFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";

async function testQueueLatestRefreshesKeyedState(page) {
  await installFakeServer(page, {
    actionResponses: [
      {
        body: `<mutation heimdall-content-target="#keyed-state-host">
                 <mutation-attr name="data-heimdall-state-row" value='{"count":1}'></mutation-attr>
               </mutation>
               <span id="keyed-first">Keyed 1</span>`,
        delayMs: 40
      },
      {
        body: `<mutation heimdall-content-target="#keyed-state-host">
                 <mutation-attr name="data-heimdall-state-row" value='{"count":2}'></mutation-attr>
               </mutation>
               <span id="keyed-second">Keyed 2</span>`
      }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <div id="keyed-state-host"
           data-heimdall-state='{"count":90}'
           data-heimdall-state-row='{"count":0}'>
        <button id="keyed-state-action"
                heimdall-content-click="State.Keyed"
                heimdall-content-target="#keyed-target"
                heimdall-payload-from="closest-state:row"
                heimdall-content-disable="false"
                heimdall-sync="queue-latest"
                heimdall-sync-group="keyed-state">Increment</button>
      </div>
      <div id="keyed-target">Keyed 0</div>`;

    let resolveStarted;
    let resolveFinished;
    const started = new Promise(resolve => { resolveStarted = resolve; });
    const finished = new Promise(resolve => { resolveFinished = resolve; });
    let finallyCount = 0;
    const beforeCounts = [];
    document.addEventListener("heimdall:request-before", event => {
      if (event.detail.actionId !== "State.Keyed")
        return;
      beforeCounts.push(event.detail.payload.count);
      if (beforeCounts.length === 1)
        resolveStarted();
    });
    document.addEventListener("heimdall:request-finally", event => {
      if (event.detail.actionId === "State.Keyed" && ++finallyCount === 2)
        resolveFinished();
    });

    const button = document.querySelector("#keyed-state-action");
    button.click();
    await started;
    button.click();
    await finished;

    const host = document.querySelector("#keyed-state-host");
    return {
      beforeCounts,
      keyed: JSON.parse(host.getAttribute("data-heimdall-state-row")),
      unkeyed: JSON.parse(host.getAttribute("data-heimdall-state")),
      targetHtml: document.querySelector("#keyed-target").innerHTML.trim()
    };
  });

  assert.deepEqual(state, {
    beforeCounts: [0, 1],
    keyed: { count: 2 },
    unkeyed: { count: 90 },
    targetHtml: '<span id="keyed-second">Keyed 2</span>'
  });
  const actions = actionFetches(await getFetches(page));
  assert.deepEqual(actions.map(action => action.jsonBody), [{ count: 0 }, { count: 1 }]);
}

async function testQueueLatestInPlacePayloadConfigOverride(page) {
  await installFakeServer(page, {
    actionResponses: [
      {
        body: `<mutation heimdall-content-target="#in-place-state">
                 <mutation-attr name="data-heimdall-state" value='{"count":1}'></mutation-attr>
               </mutation>`,
        delayMs: 40
      },
      { body: '<span id="in-place-complete">Configured in place</span>' }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <div id="in-place-state" data-heimdall-state='{"count":0}'>
        <button id="in-place-action"
                heimdall-content-click="State.InPlace"
                heimdall-content-target="#in-place-target"
                heimdall-payload-from="closest-state"
                heimdall-content-disable="false"
                heimdall-sync="queue-latest"
                heimdall-sync-group="in-place-state">Run</button>
      </div>
      <div id="in-place-blocker"></div>
      <div id="in-place-target"></div>`;

    let resolveBlockerStarted;
    let resolveQueuedFinished;
    const blockerStarted = new Promise(resolve => { resolveBlockerStarted = resolve; });
    const queuedFinished = new Promise(resolve => { resolveQueuedFinished = resolve; });
    let beforePayload = null;
    document.addEventListener("heimdall:request-config", event => {
      if (event.detail.actionId !== "State.InPlace")
        return;
      event.detail.payload.count = 77;
      event.detail.payload.configured = true;
    });
    document.addEventListener("heimdall:request-before", event => {
      if (event.detail.actionId === "State.InPlace.Blocker")
        resolveBlockerStarted();
      if (event.detail.actionId === "State.InPlace")
        beforePayload = { ...event.detail.payload };
    });
    document.addEventListener("heimdall:request-finally", event => {
      if (event.detail.actionId === "State.InPlace")
        resolveQueuedFinished();
    });

    const blocker = window.Heimdall.invoke("State.InPlace.Blocker", {}, {
      target: "#in-place-blocker",
      swap: "none",
      sync: "queue-latest",
      syncGroup: "in-place-state"
    });
    await blockerStarted;
    document.querySelector("#in-place-action").click();
    await Promise.all([blocker, queuedFinished]);

    return {
      beforePayload,
      currentState: JSON.parse(document.querySelector("#in-place-state").getAttribute("data-heimdall-state"))
    };
  });

  assert.deepEqual(state, {
    beforePayload: { count: 77, configured: true },
    currentState: { count: 1 }
  });
  const actions = actionFetches(await getFetches(page));
  assert.deepEqual(actions[1].jsonBody, { count: 77, configured: true });
}

async function testQueueLatestRemovedStateAttribute(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: `<mutation heimdall-content-target="#removed-state-host">
               <mutation-attr name="data-heimdall-state"></mutation-attr>
             </mutation>`,
      delayMs: 40
    }]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <div id="removed-state-host" data-heimdall-state='{"count":0}'>
        <button id="removed-state-action"
                heimdall-content-click="State.Removed"
                heimdall-content-target="#removed-state-target"
                heimdall-payload-from="closest-state"
                heimdall-content-disable="false"
                heimdall-sync="queue-latest"
                heimdall-sync-group="removed-state">Run</button>
      </div>
      <div id="removed-state-blocker"></div>
      <div id="removed-state-target"></div>`;

    let resolveBlockerStarted;
    let resolveQueuedFinished;
    const blockerStarted = new Promise(resolve => { resolveBlockerStarted = resolve; });
    const queuedFinished = new Promise(resolve => { resolveQueuedFinished = resolve; });
    const cancellations = [];
    document.addEventListener("heimdall:request-before", event => {
      if (event.detail.actionId === "State.Removed.Blocker")
        resolveBlockerStarted();
    });
    document.addEventListener("heimdall:request-cancel", event => {
      if (event.detail.actionId === "State.Removed")
        cancellations.push(event.detail.result.cancelReason);
    });
    document.addEventListener("heimdall:request-finally", event => {
      if (event.detail.actionId === "State.Removed")
        resolveQueuedFinished();
    });

    const blocker = window.Heimdall.invoke("State.Removed.Blocker", {}, {
      target: "#removed-state-blocker",
      swap: "none",
      sync: "queue-latest",
      syncGroup: "removed-state"
    });
    await blockerStarted;
    document.querySelector("#removed-state-action").click();
    await Promise.all([blocker, queuedFinished]);

    return {
      cancellations,
      hostConnected: document.querySelector("#removed-state-host").isConnected,
      hasState: document.querySelector("#removed-state-host").hasAttribute("data-heimdall-state")
    };
  });

  assert.deepEqual(state, {
    cancellations: ["payload-source-unavailable"],
    hostConnected: true,
    hasState: false
  });
  assert.equal(actionFetches(await getFetches(page)).length, 1);
}

async function testQueueLatestUsesLatestSourceBinding(page) {
  await installFakeServer(page, {
    actionResponses: [
      {
        body: `<mutation heimdall-content-target="#latest-third-host">
                 <mutation-attr name="data-heimdall-state" value='{"source":"third","version":1}'></mutation-attr>
               </mutation>`,
        delayMs: 40
      },
      { body: '<span id="latest-source-complete">Latest source</span>' }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <div id="latest-second-host" data-heimdall-state='{"source":"second","version":0}'>
        <button id="latest-second"
                heimdall-content-click="State.SourceSecond"
                heimdall-content-target="#latest-target"
                heimdall-payload-from="closest-state"
                heimdall-content-disable="false"
                heimdall-sync="queue-latest"
                heimdall-sync-group="latest-source">Second</button>
      </div>
      <div id="latest-third-host" data-heimdall-state='{"source":"third","version":0}'>
        <button id="latest-third"
                heimdall-content-click="State.SourceThird"
                heimdall-content-target="#latest-target"
                heimdall-payload-from="closest-state"
                heimdall-content-disable="false"
                heimdall-sync="queue-latest"
                heimdall-sync-group="latest-source">Third</button>
      </div>
      <div id="latest-blocker"></div>
      <div id="latest-target"></div>`;

    let resolveBlockerStarted;
    let resolveThirdFinished;
    const blockerStarted = new Promise(resolve => { resolveBlockerStarted = resolve; });
    const thirdFinished = new Promise(resolve => { resolveThirdFinished = resolve; });
    const cancellations = [];
    const beforeActions = [];
    document.addEventListener("heimdall:request-before", event => {
      if (event.detail.actionId === "State.SourceBlocker")
        resolveBlockerStarted();
      if (event.detail.actionId.startsWith("State.Source"))
        beforeActions.push(event.detail.actionId);
    });
    document.addEventListener("heimdall:request-cancel", event => {
      if (event.detail.actionId === "State.SourceSecond")
        cancellations.push(event.detail.result.cancelReason);
    });
    document.addEventListener("heimdall:request-finally", event => {
      if (event.detail.actionId === "State.SourceThird")
        resolveThirdFinished();
    });

    const blocker = window.Heimdall.invoke("State.SourceBlocker", {}, {
      target: "#latest-blocker",
      swap: "none",
      sync: "queue-latest",
      syncGroup: "latest-source"
    });
    await blockerStarted;
    document.querySelector("#latest-second").click();
    document.querySelector("#latest-third").click();
    await Promise.all([blocker, thirdFinished]);

    return {
      cancellations,
      beforeActions,
      latestState: JSON.parse(document.querySelector("#latest-third-host").getAttribute("data-heimdall-state"))
    };
  });

  assert.deepEqual(state, {
    cancellations: ["queue-replaced"],
    beforeActions: ["State.SourceBlocker", "State.SourceThird"],
    latestState: { source: "third", version: 1 }
  });
  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 2);
  assert.deepEqual(actions[1].jsonBody, { source: "third", version: 1 });
}

async function testClosestStateCsrfRetrySnapshot(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-first", "csrf-retry"],
    actionResponses: [
      {
        status: 400,
        body: "Antiforgery token validation failed.",
        delayMs: 40
      },
      { body: '<span id="state-retry-complete">Retried</span>' }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <div id="retry-state-host" data-heimdall-state='{"count":0}'>
        <button id="retry-state-action"
                heimdall-content-click="State.Retry"
                heimdall-content-target="#retry-target"
                heimdall-payload-from="closest-state">Retry</button>
      </div>
      <div id="retry-target"></div>`;

    document.addEventListener("heimdall:request-before", event => {
      if (event.detail.actionId !== "State.Retry" || event.detail.attempt !== 1)
        return;
      setTimeout(() => {
        document.querySelector("#retry-state-host")
          .setAttribute("data-heimdall-state", '{"count":1}');
      }, 10);
    });

    document.querySelector("#retry-state-action").click();
    const started = performance.now();
    while (!document.querySelector("#state-retry-complete")) {
      if (performance.now() - started > 5000)
        throw new Error("Timed out waiting for the state retry.");
      await new Promise(resolve => setTimeout(resolve, 10));
    }

    return JSON.parse(document.querySelector("#retry-state-host").getAttribute("data-heimdall-state"));
  });

  assert.deepEqual(state, { count: 1 });
  const fetches = await getFetches(page);
  const actions = actionFetches(fetches);
  assert.equal(actions.length, 2);
  assert.deepEqual(actions.map(action => action.jsonBody), [{ count: 0 }, { count: 0 }]);
  assert.equal(csrfFetches(fetches).length, 2);
}

export const tests = [
  ["refreshes keyed closest state when a queued request begins", testQueueLatestRefreshesKeyedState],
  ["preserves in-place request-config payload changes for queued state", testQueueLatestInPlacePayloadConfigOverride],
  ["cancels queued requests when their closest-state attribute is removed", testQueueLatestRemovedStateAttribute],
  ["keeps the latest queued payload bound to its own state source", testQueueLatestUsesLatestSourceBinding],
  ["freezes refreshed state across an antiforgery retry", testClosestStateCsrfRetrySnapshot]
];
