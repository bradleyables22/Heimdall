import assert from "node:assert/strict";
import {
  actionFetches,
  csrfFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";

async function testQueueLatestFormAndFileSnapshot(page) {
  await installFakeServer(page, {
    actionResponses: [
      { body: "", delayMs: 180 },
      { body: '<span id="queued-upload-complete">Uploaded</span>' }
    ]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <form id="queued-upload"
            heimdall-content-submit="Upload.Queued"
            heimdall-content-target="#upload-target"
            heimdall-content-disable="false"
            heimdall-sync="queue-latest"
            heimdall-sync-group="upload-snapshot">
        <input id="queued-caption" name="caption" value="original caption">
        <input id="queued-file" name="attachment" type="file">
        <button type="submit">Upload</button>
      </form>
      <div id="blocker"></div>
      <div id="upload-target"></div>`;
  });
  await page.setInputFiles("#queued-file", {
    name: "original.txt",
    mimeType: "text/plain",
    buffer: Buffer.from("original file")
  });

  await page.evaluate(async () => {
    let resolveBlockerStarted;
    const blockerStarted = new Promise(resolve => { resolveBlockerStarted = resolve; });
    window.__queuedUploadFinished = new Promise(resolve => {
      document.addEventListener("heimdall:request-finally", event => {
        if (event.detail.actionId === "Upload.Queued")
          resolve();
      });
    });
    document.addEventListener("heimdall:request-before", event => {
      if (event.detail.actionId === "Upload.Blocker")
        resolveBlockerStarted();
    });

    window.__uploadBlocker = window.Heimdall.invoke("Upload.Blocker", {}, {
      target: "#blocker",
      swap: "none",
      sync: "queue-latest",
      syncGroup: "upload-snapshot"
    });
    await blockerStarted;
    document.querySelector("#queued-upload").requestSubmit();
  });

  await page.fill("#queued-caption", "edited after submit");
  await page.setInputFiles("#queued-file", {
    name: "replacement.txt",
    mimeType: "text/plain",
    buffer: Buffer.from("replacement file")
  });
  await page.evaluate(() => Promise.all([window.__uploadBlocker, window.__queuedUploadFinished]));

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 2);
  assert.deepEqual(actions[1].formBody, [
    { name: "caption", value: "original caption" },
    {
      name: "attachment",
      value: { fileName: "original.txt", size: 13, type: "text/plain" }
    }
  ]);
  assert.equal(await page.locator("#upload-target").innerHTML(), '<span id="queued-upload-complete">Uploaded</span>');
}

async function testQueueLatestReplacedStateSource(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: '<div id="state-source" data-heimdall-state=\'{"count":1}\'>Replacement state host</div>',
      delayMs: 40
    }]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <div id="state-source" data-heimdall-state='{"count":0}'>
        <button id="stale-state-action"
                heimdall-content-click="State.Stale"
                heimdall-content-target="#result"
                heimdall-payload-from="closest-state"
                heimdall-content-disable="false"
                heimdall-sync="queue-latest"
                heimdall-sync-group="replaced-state">Run</button>
      </div>
      <div id="result"></div>`;

    let resolveStarted;
    let resolveQueuedFinished;
    const started = new Promise(resolve => { resolveStarted = resolve; });
    const queuedFinished = new Promise(resolve => { resolveQueuedFinished = resolve; });
    const cancellations = [];
    document.addEventListener("heimdall:request-before", event => {
      if (event.detail.actionId === "State.ReplaceHost")
        resolveStarted();
    });
    document.addEventListener("heimdall:request-cancel", event => {
      if (event.detail.actionId === "State.Stale")
        cancellations.push(event.detail.result.cancelReason);
    });
    document.addEventListener("heimdall:request-finally", event => {
      if (event.detail.actionId === "State.Stale")
        resolveQueuedFinished();
    });

    const first = window.Heimdall.invoke("State.ReplaceHost", {}, {
      target: "#state-source",
      swap: "outer",
      sync: "queue-latest",
      syncGroup: "replaced-state"
    });
    await started;
    document.querySelector("#stale-state-action").click();
    await Promise.all([first, queuedFinished]);

    return {
      cancellations,
      replacementText: document.querySelector("#state-source").textContent.trim()
    };
  });

  assert.deepEqual(state, {
    cancellations: ["payload-source-unavailable"],
    replacementText: "Replacement state host"
  });
  assert.equal(actionFetches(await getFetches(page)).length, 1);
}

export const tests = [
  ["keeps queued form fields and files captured at submission", testQueueLatestFormAndFileSnapshot],
  ["cancels queued requests whose closest-state source was replaced", testQueueLatestReplacedStateSource]
];
