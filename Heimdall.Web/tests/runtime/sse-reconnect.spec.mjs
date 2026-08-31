import assert from "node:assert/strict";
import {
  actionFetches,
  csrfFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";

async function testSseTokenFailureRetry(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-sse-retry"],
    bifrostTokenResponses: [
      { status: 401, body: "try again" },
      { token: "st-retry" }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="sse-host"></div>';
    window.Heimdall.config.sseReconnectDelayMs = 10;
    window.Heimdall.config.sseReconnectMaxDelayMs = 10;

    window.__eventSources = [];
    window.EventSource = class {
      constructor(url) {
        this.url = url;
        this.closed = false;
        window.__eventSources.push(this);
      }

      addEventListener() {
      }

      close() {
        this.closed = true;
      }
    };

    const scheduled = [];
    document.addEventListener("heimdall:sse-reconnect-scheduled", ev => {
      scheduled.push({
        topic: ev.detail.topic,
        reason: ev.detail.reason,
        status: ev.detail.status,
        delayMs: ev.detail.delayMs
      });
    });

    window.Heimdall.sse.connect("topic:retry", {
      element: document.querySelector("#sse-host"),
      event: "message"
    });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        if (window.__eventSources.length > 0) {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for retry EventSource"));
        }
      }, 10);
    });

    return {
      eventSourceUrl: window.__eventSources[0].url,
      scheduled
    };
  });

  const eventSourceUrl = new URL(state.eventSourceUrl);
  assert.equal(eventSourceUrl.searchParams.get("topic"), "topic:retry");
  assert.equal(eventSourceUrl.searchParams.get("st"), "st-retry");
  assert.equal(state.scheduled.length, 1);
  assert.deepEqual(state.scheduled[0], {
    topic: "topic:retry",
    reason: "token-failed",
    status: 401,
    delayMs: 10
  });

  const fetches = await getFetches(page);
  const tokenFetches = fetches.filter(fetch => fetch.url.includes("/__heimdall/v1/bifrost/token"));
  assert.equal(tokenFetches.length, 2);
}

async function testSseErrorReconnectFreshToken(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-sse-error"],
    bifrostTokenResponses: [
      { token: "st-first" },
      { token: "st-second" }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="sse-host"></div>';
    window.Heimdall.config.sseReconnectDelayMs = 10;
    window.Heimdall.config.sseReconnectMaxDelayMs = 10;

    window.__eventSources = [];
    window.EventSource = class {
      constructor(url) {
        this.url = url;
        this.closed = false;
        window.__eventSources.push(this);
      }

      addEventListener() {
      }

      close() {
        this.closed = true;
      }
    };

    const scheduled = [];
    document.addEventListener("heimdall:sse-reconnect-scheduled", ev => {
      scheduled.push({
        topic: ev.detail.topic,
        reason: ev.detail.reason,
        delayMs: ev.detail.delayMs
      });
    });

    window.Heimdall.sse.connect("topic:error", {
      element: document.querySelector("#sse-host"),
      event: "message"
    });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        const es = window.__eventSources[0];
        if (es && typeof es.onerror === "function") {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for initial EventSource"));
        }
      }, 10);
    });

    const first = window.__eventSources[0];
    first.onerror({ type: "error" });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        if (window.__eventSources.length > 1) {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for reconnect EventSource"));
        }
      }, 10);
    });

    return {
      firstUrl: window.__eventSources[0].url,
      secondUrl: window.__eventSources[1].url,
      firstClosed: window.__eventSources[0].closed,
      scheduled
    };
  });

  const firstUrl = new URL(state.firstUrl);
  const secondUrl = new URL(state.secondUrl);

  assert.equal(firstUrl.searchParams.get("st"), "st-first");
  assert.equal(secondUrl.searchParams.get("st"), "st-second");
  assert.equal(state.firstClosed, true);
  assert.equal(state.scheduled.length, 1);
  assert.deepEqual(state.scheduled[0], {
    topic: "topic:error",
    reason: "eventsource-error",
    delayMs: 10
  });

  const fetches = await getFetches(page);
  const tokenFetches = fetches.filter(fetch => fetch.url.includes("/__heimdall/v1/bifrost/token"));
  assert.equal(tokenFetches.length, 2);
}

async function testSseOfflinePauseResume(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-sse-offline"],
    bifrostTokenResponses: [
      { token: "st-offline-first" },
      { token: "st-offline-second" }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="sse-host"></div>';
    window.Heimdall.config.sseReconnectDelayMs = 10;
    window.Heimdall.config.sseReconnectMaxDelayMs = 10;
    window.__heimdallOnline = true;

    Object.defineProperty(window.navigator, "onLine", {
      configurable: true,
      get: () => window.__heimdallOnline
    });

    window.__eventSources = [];
    window.EventSource = class {
      constructor(url) {
        this.url = url;
        this.closed = false;
        window.__eventSources.push(this);
      }

      addEventListener() {
      }

      close() {
        this.closed = true;
      }
    };

    const paused = [];
    const resumed = [];
    const scheduled = [];

    document.addEventListener("heimdall:sse-pause", ev => {
      paused.push({
        topic: ev.detail.topic,
        reason: ev.detail.reason
      });
    });

    document.addEventListener("heimdall:sse-resume", ev => {
      resumed.push({
        topic: ev.detail.topic,
        reason: ev.detail.reason,
        previousReason: ev.detail.previousReason
      });
    });

    document.addEventListener("heimdall:sse-reconnect-scheduled", ev => {
      scheduled.push({
        topic: ev.detail.topic,
        reason: ev.detail.reason
      });
    });

    window.Heimdall.sse.connect("topic:offline", {
      element: document.querySelector("#sse-host"),
      event: "message"
    });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        const es = window.__eventSources[0];
        if (es && typeof es.onerror === "function") {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for initial EventSource"));
        }
      }, 10);
    });

    window.__heimdallOnline = false;
    window.__eventSources[0].onerror({ type: "error" });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        if (paused.length > 0 && window.__eventSources[0].closed) {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for offline pause"));
        }
      }, 10);
    });

    await new Promise(resolve => setTimeout(resolve, 50));
    const countWhileOffline = window.__eventSources.length;

    window.__heimdallOnline = true;
    window.dispatchEvent(new Event("online"));

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        if (window.__eventSources.length > 1) {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for online resume"));
        }
      }, 10);
    });

    return {
      firstUrl: window.__eventSources[0].url,
      secondUrl: window.__eventSources[1].url,
      firstClosed: window.__eventSources[0].closed,
      countWhileOffline,
      paused,
      resumed,
      scheduled
    };
  });

  const firstUrl = new URL(state.firstUrl);
  const secondUrl = new URL(state.secondUrl);

  assert.equal(firstUrl.searchParams.get("st"), "st-offline-first");
  assert.equal(secondUrl.searchParams.get("st"), "st-offline-second");
  assert.equal(state.firstClosed, true);
  assert.equal(state.countWhileOffline, 1);
  assert.deepEqual(state.paused, [{ topic: "topic:offline", reason: "offline" }]);
  assert.deepEqual(state.resumed, [{ topic: "topic:offline", reason: "online", previousReason: "offline" }]);
  assert.deepEqual(state.scheduled, []);

  const fetches = await getFetches(page);
  const tokenFetches = fetches.filter(fetch => fetch.url.includes("/__heimdall/v1/bifrost/token"));
  assert.equal(tokenFetches.length, 2);
}

export const tests = [
  ["retries SSE token failures with backoff", testSseTokenFailureRetry],
  ["reconnects SSE errors with a fresh token", testSseErrorReconnectFreshToken],
  ["pauses SSE reconnects while offline", testSseOfflinePauseResume]
];
