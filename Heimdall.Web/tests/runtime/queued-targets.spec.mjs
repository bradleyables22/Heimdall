import assert from "node:assert/strict";
import {
  actionFetches,
  csrfFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";

async function testQueueLatestTargetReplacement(page) {
  await installFakeServer(page, {
    actionResponses: [
      { body: '<div id="target"><span id="target-first">First</span></div>', delayMs: 40 },
      { body: '<span id="target-second">Second</span>' }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target">Original</div>';
    const original = document.querySelector("#target");
    let resolveStarted;
    const started = new Promise(resolve => { resolveStarted = resolve; });
    document.addEventListener("heimdall:request-before", event => {
      if (event.detail.actionId === "Target.First")
        resolveStarted();
    });

    const first = window.Heimdall.invoke("Target.First", {}, {
      target: "#target",
      swap: "outer",
      sync: "queue-latest",
      syncGroup: "target-replacement"
    });
    await started;
    const second = window.Heimdall.invoke("Target.Second", {}, {
      target: "#target",
      swap: "inner",
      sync: "queue-latest",
      syncGroup: "target-replacement"
    });

    const [firstResult, secondResult] = await Promise.all([first, second]);
    const current = document.querySelector("#target");
    return {
      firstOk: firstResult.ok,
      secondOk: secondResult.ok,
      originalConnected: original.isConnected,
      originalHtml: original.innerHTML,
      currentHtml: current.innerHTML,
      currentIsOriginal: current === original
    };
  });

  assert.deepEqual(state, {
    firstOk: true,
    secondOk: true,
    originalConnected: false,
    originalHtml: "Original",
    currentHtml: '<span id="target-second">Second</span>',
    currentIsOriginal: false
  });
}

async function testOobTargetReplacementBeforeMainSwap(page) {
  await installFakeServer(page, {
    actionResponses: [
      {
        body: `<invocation heimdall-content-target="#target" heimdall-content-swap="outer">
                 <template><div id="target">OOB replacement</div></template>
               </invocation>
               <span id="main-after-oob">Main response</span>`
      },
      {
        body: `<invocation heimdall-content-target="#late-host">
                 <template><div id="late-target">Created by OOB</div></template>
               </invocation>
               <span id="main-after-create">Main after create</span>`
      }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target">Original</div>';
    const original = document.querySelector("#target");
    const result = await window.Heimdall.invoke("Target.Oob", {}, { target: "#target" });
    const current = document.querySelector("#target");
    return {
      ok: result.ok,
      originalConnected: original.isConnected,
      originalHtml: original.innerHTML,
      currentHtml: current.innerHTML.trim()
    };
  });

  assert.deepEqual(state, {
    ok: true,
    originalConnected: false,
    originalHtml: "Original",
    currentHtml: '<span id="main-after-oob">Main response</span>'
  });

  const createdState = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="late-host"></div>';
    const result = await window.Heimdall.invoke("Target.OobCreate", {}, { target: "#late-target" });
    return {
      ok: result.ok,
      targetHtml: document.querySelector("#late-target")?.innerHTML.trim() || null
    };
  });

  assert.deepEqual(createdState, {
    ok: true,
    targetHtml: '<span id="main-after-create">Main after create</span>'
  });
}

async function testQueueLatestDisconnectedTarget(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: "", delayMs: 40 }]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="active-target"></div><div id="obsolete-target"></div>';
    const obsolete = document.querySelector("#obsolete-target");
    const cancellations = [];
    let resolveStarted;
    const started = new Promise(resolve => { resolveStarted = resolve; });

    document.addEventListener("heimdall:request-before", event => {
      if (event.detail.actionId === "Target.Active")
        resolveStarted();
    });
    document.addEventListener("heimdall:request-cancel", event => {
      if (event.detail.actionId === "Target.Obsolete")
        cancellations.push(event.detail.result.cancelReason);
    });

    const active = window.Heimdall.invoke("Target.Active", {}, {
      target: "#active-target",
      swap: "none",
      sync: "queue-latest",
      syncGroup: "direct-target"
    });
    await started;
    const queued = window.Heimdall.invoke("Target.Obsolete", {}, {
      target: obsolete,
      sync: "queue-latest",
      syncGroup: "direct-target"
    });
    obsolete.remove();

    const [activeResult, queuedResult] = await Promise.all([active, queued]);
    return {
      activeOk: activeResult.ok,
      queuedCancelled: queuedResult.cancelled,
      queuedReason: queuedResult.cancelReason,
      cancellations
    };
  });

  assert.deepEqual(state, {
    activeOk: true,
    queuedCancelled: true,
    queuedReason: "target-disconnected",
    cancellations: ["target-disconnected"]
  });
  assert.equal(actionFetches(await getFetches(page)).length, 1);
}

async function testRequestBeforeTargetOverrideAcrossOob(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: `<invocation heimdall-content-target="#override-target" heimdall-content-swap="outer">
               <template><div id="override-target">OOB replacement</div></template>
             </invocation>
             <span id="override-main">Main override</span>`
    }]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <div id="original-target">Original</div>
      <div id="override-target">Override original</div>`;
    const originalOverride = document.querySelector("#override-target");
    document.addEventListener("heimdall:request-before", event => {
      if (event.detail.actionId === "Target.BeforeOverride")
        event.detail.target = "#override-target";
    });

    const result = await window.Heimdall.invoke("Target.BeforeOverride", {}, {
      target: "#original-target"
    });
    const currentOverride = document.querySelector("#override-target");
    return {
      ok: result.ok,
      originalText: document.querySelector("#original-target").textContent,
      originalOverrideConnected: originalOverride.isConnected,
      overrideIsOriginal: currentOverride === originalOverride,
      overrideHtml: currentOverride.innerHTML.trim()
    };
  });

  assert.deepEqual(state, {
    ok: true,
    originalText: "Original",
    originalOverrideConnected: false,
    overrideIsOriginal: false,
    overrideHtml: '<span id="override-main">Main override</span>'
  });
}

export const tests = [
  ["re-resolves selector targets replaced before a queued request begins", testQueueLatestTargetReplacement],
  ["re-resolves selector targets replaced by OOB before the main swap", testOobTargetReplacementBeforeMainSwap],
  ["cancels queued requests whose direct target was disconnected", testQueueLatestDisconnectedTarget],
  ["preserves request-before target overrides across OOB replacement", testRequestBeforeTargetOverrideAcrossOob]
];
