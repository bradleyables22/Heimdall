import assert from "node:assert/strict";
import {
  actionFetches,
  csrfFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";

async function testQueueLatestRefreshesClosestState(page) {
  await installFakeServer(page, {
    actionResponses: [
      {
        body: `<mutation heimdall-content-target="#state-host">
                 <mutation-attr name="data-heimdall-state" value='{"count":1}'></mutation-attr>
               </mutation>
               <span id="state-first">Count 1</span>`,
        delayMs: 40
      },
      {
        body: `<mutation heimdall-content-target="#state-host">
                 <mutation-attr name="data-heimdall-state" value='{"count":2}'></mutation-attr>
               </mutation>
               <span id="state-second">Count 2</span>`
      }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <div id="state-host" data-heimdall-state='{"count":0}'>
        <button id="state-action"
                heimdall-content-click="State.Increment"
                heimdall-content-target="#target"
                heimdall-payload-from="closest-state"
                heimdall-content-disable="false"
                heimdall-sync="queue-latest"
                heimdall-sync-group="state-chain">Increment</button>
      </div>
      <div id="target">Count 0</div>`;

    const beforePayloads = [];
    let resolveFirstStarted;
    let resolveCompleted;
    const firstStarted = new Promise(resolve => { resolveFirstStarted = resolve; });
    const completed = new Promise(resolve => { resolveCompleted = resolve; });
    let finallyCount = 0;

    document.addEventListener("heimdall:request-before", event => {
      if (event.detail.actionId !== "State.Increment")
        return;
      beforePayloads.push(event.detail.payload.count);
      if (beforePayloads.length === 1)
        resolveFirstStarted();
    });
    document.addEventListener("heimdall:request-finally", event => {
      if (event.detail.actionId === "State.Increment" && ++finallyCount === 2)
        resolveCompleted();
    });

    const button = document.querySelector("#state-action");
    button.click();
    await firstStarted;
    button.click();
    await completed;

    return {
      beforePayloads,
      finalState: JSON.parse(document.querySelector("#state-host").getAttribute("data-heimdall-state")),
      targetHtml: document.querySelector("#target").innerHTML
    };
  });

  assert.deepEqual(state.beforePayloads, [0, 1]);
  assert.deepEqual(state.finalState, { count: 2 });
  assert.match(state.targetHtml, /state-second/);

  const actions = actionFetches(await getFetches(page));
  assert.deepEqual(actions.map(action => action.jsonBody), [{ count: 0 }, { count: 1 }]);
}

async function testQueueLatestPayloadConfigOverride(page) {
  await installFakeServer(page, {
    actionResponses: [
      {
        body: `<mutation heimdall-content-target="#config-state">
                 <mutation-attr name="data-heimdall-state" value='{"count":1}'></mutation-attr>
               </mutation>`,
        delayMs: 40
      },
      { body: '<span id="configured-queued">Configured queued payload</span>' }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <div id="config-state" data-heimdall-state='{"count":0}'>
        <button id="configured-action"
                heimdall-content-click="State.Configured"
                heimdall-content-target="#configured-target"
                heimdall-payload-from="closest-state"
                heimdall-content-disable="false"
                heimdall-sync="queue-latest"
                heimdall-sync-group="configured-state">Run</button>
      </div>
      <div id="blocker-target"></div>
      <div id="configured-target"></div>`;

    let resolveBlockerStarted;
    let resolveQueuedFinished;
    const blockerStarted = new Promise(resolve => { resolveBlockerStarted = resolve; });
    const queuedFinished = new Promise(resolve => { resolveQueuedFinished = resolve; });
    const beforePayloads = [];

    document.addEventListener("heimdall:request-config", event => {
      if (event.detail.actionId === "State.Configured")
        event.detail.payload = { count: 99, configured: true };
    });
    document.addEventListener("heimdall:request-before", event => {
      if (event.detail.actionId === "State.Blocker")
        resolveBlockerStarted();
      if (event.detail.actionId === "State.Configured")
        beforePayloads.push(event.detail.payload);
    });
    document.addEventListener("heimdall:request-finally", event => {
      if (event.detail.actionId === "State.Configured")
        resolveQueuedFinished();
    });

    const blocker = window.Heimdall.invoke("State.Blocker", {}, {
      target: "#blocker-target",
      swap: "none",
      sync: "queue-latest",
      syncGroup: "configured-state"
    });
    await blockerStarted;
    document.querySelector("#configured-action").click();
    await Promise.all([blocker, queuedFinished]);

    return {
      beforePayloads,
      currentState: JSON.parse(document.querySelector("#config-state").getAttribute("data-heimdall-state"))
    };
  });

  assert.deepEqual(state.beforePayloads, [{ count: 99, configured: true }]);
  assert.deepEqual(state.currentState, { count: 1 });

  const actions = actionFetches(await getFetches(page));
  assert.deepEqual(actions[1].jsonBody, { count: 99, configured: true });
}

export const tests = [
  ["refreshes closest state when a queued request begins", testQueueLatestRefreshesClosestState],
  ["keeps request-config payload overrides authoritative for queued state", testQueueLatestPayloadConfigOverride]
];
