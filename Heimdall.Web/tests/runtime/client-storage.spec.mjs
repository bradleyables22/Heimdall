import assert from "node:assert/strict";
import {
  actionFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";

async function testStorageClientInfoAllowlistAndFreshReads(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: "" }, { body: "" }]
  });

  await page.evaluate(async () => {
    localStorage.setItem("ui.theme", JSON.stringify("dark"));
    localStorage.setItem("ignored", JSON.stringify("should not be sent"));
    sessionStorage.setItem("checkout.state", JSON.stringify({ step: 2 }));
    window.Heimdall.config.clientInfo = {
      enabled: true,
      storage: {
        local: ["ui.theme"],
        session: ["checkout.state"]
      }
    };

    await window.Heimdall.invoke("Storage.Info.First", null, { swap: "none" });

    localStorage.setItem("ui.theme", JSON.stringify("light"));
    sessionStorage.setItem("checkout.state", JSON.stringify({ step: 3 }));
    await window.Heimdall.invoke("Storage.Info.Second", null, { swap: "none" });
  });

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 2);
  assert.deepEqual(JSON.parse(actions[0].headers["x-heimdall-client-info"]).storage, {
    local: { "ui.theme": '\"dark\"' },
    session: { "checkout.state": '{"step":2}' }
  });
  assert.deepEqual(JSON.parse(actions[1].headers["x-heimdall-client-info"]).storage, {
    local: { "ui.theme": '\"light\"' },
    session: { "checkout.state": '{"step":3}' }
  });
  assert.equal(JSON.parse(actions[0].headers["x-heimdall-client-info"]).storage.local.ignored, undefined);
}

async function testClientInfoWithoutStorageConfigurationOmitsStorage(page) {
  await installFakeServer(page, { actionResponses: [{ body: "" }] });

  await page.evaluate(async () => {
    localStorage.setItem("ui.theme", "dark");
    sessionStorage.setItem("checkout.state", "open");
    window.Heimdall.config.clientInfo = true;

    await window.Heimdall.invoke("Storage.Info.NoConfig", null, { swap: "none" });
  });

  const action = actionFetches(await getFetches(page))[0];
  const info = JSON.parse(action.headers["x-heimdall-client-info"]);
  assert.equal(info.storage, undefined);
}

async function testStorageClientInfoDiagnostics(page) {
  await installFakeServer(page, { actionResponses: [{ body: "" }] });

  const state = await page.evaluate(async () => {
    localStorage.setItem("too-long", JSON.stringify("123456"));
    window.Heimdall.config.clientInfo = {
      enabled: true,
      storage: { local: ["too-long"] }
    };
    window.Heimdall.config.clientInfoLimits = { maxValueLength: 3 };
    const errors = [];
    document.addEventListener("heimdall:client-info-error", event => {
      errors.push({
        code: event.detail.code,
        area: event.detail.area,
        key: event.detail.key,
        hasValue: Object.prototype.hasOwnProperty.call(event.detail, "value")
      });
    });

    await window.Heimdall.invoke("Storage.Info.Limit", null, { swap: "none" });
    return errors;
  });

  const action = actionFetches(await getFetches(page))[0];
  const info = JSON.parse(action.headers["x-heimdall-client-info"]);
  assert.equal(info.storage, undefined);
  assert.deepEqual(state, [{
    code: "storage-value-too-long",
    area: "local",
    key: "too-long",
    hasValue: false
  }]);
}

async function testStorageClientInfoBlockedReads(page) {
  await installFakeServer(page, { actionResponses: [{ body: "" }] });

  const state = await page.evaluate(async () => {
    const original = window.localStorage;
    Object.defineProperty(window, "localStorage", {
      configurable: true,
      value: {
        getItem() {
          throw new DOMException("Access denied", "SecurityError");
        }
      }
    });

    window.Heimdall.config.clientInfo = {
      enabled: true,
      storage: { local: ["blocked"] }
    };
    const errors = [];
    document.addEventListener("heimdall:client-info-error", event => errors.push({
      code: event.detail.code,
      area: event.detail.area,
      key: event.detail.key,
      hasValue: Object.prototype.hasOwnProperty.call(event.detail, "value")
    }));

    await window.Heimdall.invoke("Storage.Info.Blocked", null, { swap: "none" });
    return {
      errors,
      storageHeader: JSON.parse(
        window.__heimdallFetches.find(fetch => fetch.url.includes("/content/invocations"))
          .headers["x-heimdall-client-info"]).storage,
      originalStorageAvailable: !!original
    };
  });

  assert.equal(state.originalStorageAvailable, true);
  assert.equal(state.storageHeader, undefined);
  assert.deepEqual(state.errors, [{
    code: "storage-read-failed",
    area: "local",
    key: "blocked",
    hasValue: false
  }]);
}

async function testStorageClientInfoExtensionLimits(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: "" }, { body: "" }]
  });

  const state = await page.evaluate(async () => {
    window.Heimdall.config.clientInfo = true;
    window.Heimdall.config.clientInfoLimits = {
      maxExtensionEntries: 1,
      maxExtensionDepth: 1
    };
    const errors = [];
    document.addEventListener("heimdall:client-info-error", event => errors.push({
      code: event.detail.code,
      key: event.detail.key,
      hasValue: Object.prototype.hasOwnProperty.call(event.detail, "value")
    }));
    document.addEventListener("heimdall:client-info-before", event => {
      if (event.detail.actionId === "Storage.Info.ExtensionEntries") {
        event.detail.info.extensions = {
          allowed: "yes",
          omitted: "secret"
        };
      } else {
        event.detail.info.extensions = {
          nested: { deep: { value: true } }
        };
      }
    });

    await window.Heimdall.invoke("Storage.Info.ExtensionEntries", null, { swap: "none" });
    await window.Heimdall.invoke("Storage.Info.ExtensionDepth", null, { swap: "none" });

    return {
      errors,
      headers: window.__heimdallFetches
        .filter(fetch => fetch.url.includes("/content/invocations"))
        .map(fetch => JSON.parse(fetch.headers["x-heimdall-client-info"]).extensions)
    };
  });

  assert.deepEqual(state.headers, [
    { allowed: "yes" },
    {}
  ]);
  assert.deepEqual(state.errors, [
    { code: "extension-entry-limit", key: "omitted", hasValue: false },
    { code: "extension-depth-limit", key: "nested", hasValue: false }
  ]);
}

async function testStoragePayloadSourcePreservesJsonShape(page) {
  await installFakeServer(page, {
    actionResponses: [
      { body: '<span id="primitive-done">Primitive</span>' },
      { body: '<span id="object-done">Object</span>' },
      { body: '<span id="invalid-done">Invalid</span>' }
    ]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <button id="storage-payload"
              heimdall-content-click="Storage.Payload"
              heimdall-payload-from="local-storage:payload"
              heimdall-content-target="#target">Send</button>
      <div id="target">Old</div>
    `;
    localStorage.setItem("payload", JSON.stringify("value"));
  });

  await page.click("#storage-payload");
  await page.waitForSelector("#primitive-done");
  await page.evaluate(() => localStorage.setItem("payload", JSON.stringify({ value: "object" })));
  await page.click("#storage-payload");
  await page.waitForSelector("#object-done");
  await page.evaluate(() => localStorage.setItem("payload", "not-json"));
  await page.click("#storage-payload");
  await page.waitForSelector("#invalid-done");

  const actions = actionFetches(await getFetches(page));
  assert.deepEqual(actions.map(action => action.jsonBody), [
    "value",
    { value: "object" },
    {}
  ]);
}

async function testStoragePayloadQueueSnapshotAndLatestWins(page) {
  await installFakeServer(page, {
    actionResponses: [
      { body: "", delayMs: 40 },
      { body: '<span id="storage-queue-done">Queued</span>' }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <button id="storage-queue"
              heimdall-content-click="Storage.Queue"
              heimdall-payload-from="local-storage:queued"
              heimdall-content-target="#target"
              heimdall-content-disable="false"
              heimdall-sync="queue-latest"
              heimdall-sync-group="storage-queue">Queue</button>
      <div id="blocker"></div>
      <div id="target">Old</div>`;

    let resolveBlockerStarted;
    const blockerStarted = new Promise(resolve => { resolveBlockerStarted = resolve; });
    let queuedFinalCount = 0;
    let resolveQueuedFinished;
    const queuedFinished = new Promise(resolve => { resolveQueuedFinished = resolve; });
    const cancellations = [];
    document.addEventListener("heimdall:request-before", event => {
      if (event.detail.actionId === "Storage.Queue.Blocker")
        resolveBlockerStarted();
    });
    document.addEventListener("heimdall:request-cancel", event => {
      if (event.detail.actionId === "Storage.Queue")
        cancellations.push(event.detail.result.cancelReason);
    });
    document.addEventListener("heimdall:request-finally", event => {
      if (event.detail.actionId !== "Storage.Queue")
        return;
      queuedFinalCount++;
      if (queuedFinalCount === 2)
        resolveQueuedFinished();
    });

    const blocker = window.Heimdall.invoke("Storage.Queue.Blocker", {}, {
      target: "#blocker",
      swap: "none",
      sync: "queue-latest",
      syncGroup: "storage-queue"
    });
    await blockerStarted;

    localStorage.setItem("queued", JSON.stringify({ value: "first" }));
    document.querySelector("#storage-queue").click();
    localStorage.setItem("queued", JSON.stringify({ value: "second" }));
    document.querySelector("#storage-queue").click();
    localStorage.setItem("queued", JSON.stringify({ value: "third" }));

    await Promise.all([blocker, queuedFinished]);
    return {
      cancellations,
      target: document.querySelector("#target").innerHTML,
      queuedFinalCount
    };
  });

  assert.deepEqual(state, {
    cancellations: ["queue-replaced"],
    target: '<span id="storage-queue-done">Queued</span>',
    queuedFinalCount: 2
  });

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 2);
  assert.deepEqual(actions.map(action => action.jsonBody), [
    {},
    { value: "second" }
  ]);
}

async function testStorageResponseDirectivesAndAbort(page) {
  await installFakeServer(page, {
    actionResponses: [
      {
        body: '<storage area="local" operation="set" key="ui.theme">dark</storage><storage area="session" operation="set" key="checkout.state">open</storage><span id="storage-done">Done</span>'
      },
      {
        body: '<storage area="local" operation="remove" key="ui.theme"></storage><span id="storage-removed">Removed</span>'
      },
      {
        body: '<storage area="local" operation="set" key="abort.value">kept</storage><abort reason="storage-abort"></abort><span id="abort-main">Should not swap</span>'
      }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target">Original</div>';
    const events = [];
    document.addEventListener("heimdall:abort", event => events.push(event.detail.reason));

    await window.Heimdall.invoke("Storage.Set", {}, { target: "#target" });
    const afterSet = {
      local: localStorage.getItem("ui.theme"),
      session: sessionStorage.getItem("checkout.state"),
      target: document.querySelector("#target").innerHTML,
      directives: document.querySelectorAll("storage").length
    };

    await window.Heimdall.invoke("Storage.Remove", {}, { target: "#target" });
    const afterRemove = {
      local: localStorage.getItem("ui.theme"),
      target: document.querySelector("#target").innerHTML
    };

    await window.Heimdall.invoke("Storage.Abort", {}, { target: "#target" });
    return {
      afterSet,
      afterRemove,
      afterAbort: {
        abortValue: localStorage.getItem("abort.value"),
        target: document.querySelector("#target").innerHTML
      },
      events
    };
  });

  assert.deepEqual(state.afterSet, {
    local: "dark",
    session: "open",
    target: '<span id="storage-done">Done</span>',
    directives: 0
  });
  assert.deepEqual(state.afterRemove, {
    local: null,
    target: '<span id="storage-removed">Removed</span>'
  });
  assert.deepEqual(state.afterAbort, {
    abortValue: "kept",
    target: '<span id="storage-removed">Removed</span>'
  });
  assert.deepEqual(state.events, ["storage-abort"]);
}

async function testStorageDirectiveErrorsAreObservableAndNonBlocking(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: '<storage area="local" operation="set" key="bad">bad</storage><storage area="local" operation="set" key="good">good</storage><span id="storage-error-done">Applied</span>'
    }]
  });

  const state = await page.evaluate(async () => {
    const original = window.localStorage;
    Object.defineProperty(window, "localStorage", {
      configurable: true,
      value: {
        getItem: original.getItem.bind(original),
        setItem(key) {
          if (key === "bad")
            throw new DOMException("Quota exceeded", "QuotaExceededError");
          return original.setItem.call(original, key, "good");
        },
        removeItem: original.removeItem.bind(original)
      }
    });

    const errors = [];
    document.addEventListener("heimdall:storage-error", event => errors.push({
      code: event.detail.code,
      area: event.detail.area,
      operation: event.detail.operation,
      key: event.detail.key,
      hasValue: Object.prototype.hasOwnProperty.call(event.detail, "value")
    }));

    document.body.innerHTML = '<div id="target">Original</div>';
    await window.Heimdall.invoke("Storage.Errors", {}, { target: "#target" });
    return {
      errors,
      good: original.getItem("good"),
      target: document.querySelector("#target").innerHTML,
      directives: document.querySelectorAll("storage").length
    };
  });

  assert.deepEqual(state.errors, [{
    code: "storage-write-failed",
    area: "local",
    operation: "set",
    key: "bad",
    hasValue: false
  }]);
  assert.equal(state.good, "good");
  assert.equal(state.target, '<span id="storage-error-done">Applied</span>');
  assert.equal(state.directives, 0);
}

async function testMalformedStorageDirectivesAreObservableAndNonBlocking(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: '<storage area="other" operation="set" key="bad">bad</storage>' +
        '<storage area="local" operation="set">missing</storage>' +
        '<storage area="local" operation="get" key="bad"></storage>' +
        '<storage area="local" operation="set" key="good">ok</storage>' +
        '<span id="storage-malformed-done">Applied</span>'
    }]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target">Original</div>';
    const errors = [];
    document.addEventListener("heimdall:storage-error", event => errors.push({
      code: event.detail.code,
      area: event.detail.area,
      operation: event.detail.operation,
      key: event.detail.key,
      hasValue: Object.prototype.hasOwnProperty.call(event.detail, "value")
    }));

    await window.Heimdall.invoke("Storage.Malformed", {}, { target: "#target" });
    return {
      errors,
      good: localStorage.getItem("good"),
      target: document.querySelector("#target").innerHTML,
      directives: document.querySelectorAll("storage").length
    };
  });

  assert.deepEqual(state.errors, [
    { code: "invalid-area", area: "other", operation: "set", key: "bad", hasValue: false },
    { code: "missing-key", area: "local", operation: "set", key: null, hasValue: false },
    { code: "invalid-operation", area: "local", operation: "get", key: "bad", hasValue: false }
  ]);
  assert.equal(state.good, "ok");
  assert.equal(state.target, '<span id="storage-malformed-done">Applied</span>');
  assert.equal(state.directives, 0);
}

async function testStorageDirectivesSuppressOnRedirectAndError(page) {
  await installFakeServer(page, {
    actionResponses: [
      {
        body: '<storage area="local" operation="set" key="redirected">bad</storage>' +
          '<redirect url="#storage-redirect"></redirect><span>Ignored</span>'
      },
      {
        status: 500,
        body: '<storage area="local" operation="set" key="errored">bad</storage>' +
          '<span>Failure</span>'
      }
    ]
  });

  const state = await page.evaluate(async () => {
    localStorage.removeItem("redirected");
    localStorage.removeItem("errored");
    document.body.innerHTML = '<div id="target">Original</div>';

    const redirected = await window.Heimdall.invoke("Storage.Redirect", {}, { target: "#target" });
    const failed = await window.Heimdall.invoke("Storage.Error", {}, { target: "#target" });
    return {
      redirected: {
        ok: redirected.ok,
        hash: location.hash,
        value: localStorage.getItem("redirected")
      },
      failed: {
        ok: failed.ok,
        status: failed.status,
        value: localStorage.getItem("errored"),
        target: document.querySelector("#target").innerHTML
      }
    };
  });

  assert.deepEqual(state, {
    redirected: {
      ok: true,
      hash: "#storage-redirect",
      value: null
    },
    failed: {
      ok: false,
      status: 500,
      value: null,
      target: "Original"
    }
  });
}

export const tests = [
  ["sends allowlisted storage values freshly for each action", testStorageClientInfoAllowlistAndFreshReads],
  ["does not send storage without an explicit allowlist", testClientInfoWithoutStorageConfigurationOmitsStorage],
  ["reports and omits storage values that exceed client-info limits", testStorageClientInfoDiagnostics],
  ["reports blocked client-info storage reads without failing the action", testStorageClientInfoBlockedReads],
  ["bounds client-info extension entries and nesting", testStorageClientInfoExtensionLimits],
  ["uses a storage value as its complete JSON payload", testStoragePayloadSourcePreservesJsonShape],
  ["captures queued storage payloads at trigger time and keeps the latest", testStoragePayloadQueueSnapshotAndLatestWins],
  ["applies storage directives before swaps and honors abort", testStorageResponseDirectivesAndAbort],
  ["emits storage directive errors without blocking later directives or swaps", testStorageDirectiveErrorsAreObservableAndNonBlocking],
  ["reports malformed storage directives without blocking valid directives", testMalformedStorageDirectivesAreObservableAndNonBlocking],
  ["suppresses storage directives on redirects and error responses", testStorageDirectivesSuppressOnRedirectAndError]
];
