import assert from "node:assert/strict";
import {
  actionFetches,
  csrfFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";

async function testSseProgrammaticHiddenPauseResume(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-sse-hidden"],
    bifrostTokenResponses: [
      { token: "st-hidden" }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="sse-host"></div>';
    window.Heimdall.config.ssePauseWhenHidden = true;
    window.__heimdallHidden = false;

    Object.defineProperty(document, "hidden", {
      configurable: true,
      get: () => window.__heimdallHidden
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

    const handle = window.Heimdall.sse.connect("topic:hidden", {
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
          reject(new Error("Timed out waiting for initial EventSource"));
        }
      }, 10);
    });

    window.__heimdallHidden = true;
    document.dispatchEvent(new Event("visibilitychange"));

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        if (window.__eventSources[0].closed && paused.length > 0) {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for hidden pause"));
        }
      }, 10);
    });

    window.__heimdallHidden = false;
    document.dispatchEvent(new Event("visibilitychange"));

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
          reject(new Error("Timed out waiting for visible resume"));
        }
      }, 10);
    });

    return {
      firstUrl: window.__eventSources[0].url,
      secondUrl: window.__eventSources[1].url,
      firstClosed: window.__eventSources[0].closed,
      handleTopic: handle.topic,
      handleUrl: handle.url,
      paused,
      resumed
    };
  });

  const firstUrl = new URL(state.firstUrl);
  const secondUrl = new URL(state.secondUrl);

  assert.equal(firstUrl.searchParams.get("topic"), "topic:hidden");
  assert.equal(secondUrl.searchParams.get("topic"), "topic:hidden");
  assert.equal(firstUrl.searchParams.get("st"), "st-hidden");
  assert.equal(secondUrl.searchParams.get("st"), "st-hidden");
  assert.equal(state.firstClosed, true);
  assert.equal(state.handleTopic, "topic:hidden");
  assert.equal(state.handleUrl, state.secondUrl);
  assert.deepEqual(state.paused, [{ topic: "topic:hidden", reason: "hidden" }]);
  assert.deepEqual(state.resumed, [{ topic: "topic:hidden", reason: "visible", previousReason: "hidden" }]);

  const fetches = await getFetches(page);
  const tokenFetches = fetches.filter(fetch => fetch.url.includes("/__heimdall/v1/bifrost/token"));
  assert.equal(tokenFetches.length, 1);
}

async function testSseAttributeChanges(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-sse-attrs"],
    bifrostTokenResponses: [
      { token: "st-attrs-one" },
      { token: "st-attrs-two" }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="sse-host"></div><div id="target">Old</div>';
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

    const closeEvents = [];
    document.addEventListener("heimdall:sse-close", ev => {
      closeEvents.push({
        topic: ev.detail.topic,
        reason: ev.detail.reason
      });
    });

    const host = document.querySelector("#sse-host");
    host.setAttribute("heimdall-sse", "topic:attrs");
    host.setAttribute("heimdall-sse-target", "#target");
    host.setAttribute("heimdall-sse-swap", "inner");
    host.setAttribute("heimdall-sse-event", "message");

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
          reject(new Error("Timed out waiting for attribute-created EventSource"));
        }
      }, 10);
    });

    host.setAttribute("heimdall-sse", "topic:attrs-next");

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
          reject(new Error("Timed out waiting for attribute-changed EventSource"));
        }
      }, 10);
    });

    host.setAttribute("heimdall-sse-disable", "true");

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        if (window.__eventSources[1] && window.__eventSources[1].closed) {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for SSE disable close"));
        }
      }, 10);
    });

    return {
      firstUrl: window.__eventSources[0].url,
      secondUrl: window.__eventSources[1].url,
      firstClosed: window.__eventSources[0].closed,
      secondClosed: window.__eventSources[1].closed,
      closeEvents
    };
  });

  const firstUrl = new URL(state.firstUrl);
  const secondUrl = new URL(state.secondUrl);

  assert.equal(firstUrl.searchParams.get("topic"), "topic:attrs");
  assert.equal(firstUrl.searchParams.get("st"), "st-attrs-one");
  assert.equal(secondUrl.searchParams.get("topic"), "topic:attrs-next");
  assert.equal(secondUrl.searchParams.get("st"), "st-attrs-two");
  assert.equal(state.firstClosed, true);
  assert.equal(state.secondClosed, true);
  assert.deepEqual(state.closeEvents, [
    { topic: "topic:attrs", reason: "topic-changed" },
    { topic: "topic:attrs-next", reason: "disabled" }
  ]);
}

export const tests = [
  ["resumes programmatic SSE after hidden pause", testSseProgrammaticHiddenPauseResume],
  ["observes SSE attribute changes", testSseAttributeChanges]
];
