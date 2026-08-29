import assert from "node:assert/strict";
import {
  actionFetches,
  csrfFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";

async function testSseMessageSwap(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-sse-message"],
    bifrostTokens: ["st-message"]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <div id="sse-host"></div>
      <div id="target">Old</div>
    `;

    window.__eventSources = [];
    window.EventSource = class {
      constructor(url) {
        this.url = url;
        this.listeners = {};
        window.__eventSources.push(this);
      }

      addEventListener(name, handler) {
        this.listeners[name] = handler;
      }

      close() {
        this.closed = true;
      }
    };

    const seen = [];
    const swaps = [];
    document.addEventListener("heimdall:sse-message", ev => {
      seen.push({
        topic: ev.detail.topic,
        id: ev.detail.id,
        bytes: ev.detail.bytes,
        targetHtml: document.querySelector("#target").innerHTML
      });
    });
    document.addEventListener("heimdall:swap-before", event => {
      swaps.push(`before:${event.detail.origin}:${event.detail.kind}`);
    });
    document.addEventListener("heimdall:swap-after", event => {
      swaps.push(`after:${event.detail.origin}:${event.detail.kind}`);
    });

    window.Heimdall.sse.connect("topic:message", {
      element: document.querySelector("#sse-host"),
      target: "#target",
      swap: "inner",
      event: "message"
    });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        const es = window.__eventSources[0];
        if (es && typeof es.onmessage === "function") {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for EventSource message handler"));
        }
      }, 10);
    });

    const es = window.__eventSources[0];
    es.onmessage({
      data: '<span id="sse-done">SSE</span>',
      lastEventId: "evt-1"
    });

    return {
      targetHtml: document.querySelector("#target").innerHTML,
      eventSourceUrl: es.url,
      seen,
      swaps
    };
  });

  const eventSourceUrl = new URL(state.eventSourceUrl);
  assert.equal(eventSourceUrl.searchParams.get("topic"), "topic:message");
  assert.equal(eventSourceUrl.searchParams.get("st"), "st-message");
  assert.equal(state.targetHtml, '<span id="sse-done">SSE</span>');
  assert.equal(state.seen.length, 1);
  assert.equal(state.seen[0].topic, "topic:message");
  assert.equal(state.seen[0].id, "evt-1");
  assert.equal(state.seen[0].bytes, '<span id="sse-done">SSE</span>'.length);
  assert.equal(state.seen[0].targetHtml, '<span id="sse-done">SSE</span>');
  assert.deepEqual(state.swaps, ["before:sse:main", "after:sse:main"]);
}

async function testSseMutation(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-sse-mutation"],
    bifrostTokens: ["st-sse-mutation"]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <div id="sse-host"></div>
      <div id="main">Old main</div>
      <button id="side" class="pending"><span id="side-child">Side</span></button>
    `;
    const side = document.querySelector("#side");
    side.__identity = "preserved";

    window.__eventSources = [];
    window.EventSource = class {
      constructor(url) {
        this.url = url;
        this.listeners = {};
        window.__eventSources.push(this);
      }

      addEventListener(name, handler) {
        this.listeners[name] = handler;
      }

      close() {
        this.closed = true;
      }
    };

    const mutationEvents = [];
    document.addEventListener("heimdall:mutation-before", event => {
      mutationEvents.push(`before:${event.detail.origin}:${event.detail.targetSelector}`);
    });
    document.addEventListener("heimdall:mutation-after", event => {
      mutationEvents.push(`after:${event.detail.origin}:${event.detail.targetCount}`);
    });

    window.Heimdall.sse.connect("topic:mutation", {
      element: document.querySelector("#sse-host"),
      target: "#main",
      swap: "inner",
      event: "message"
    });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        const source = window.__eventSources[0];
        if (source && typeof source.onmessage === "function") {
          clearInterval(timer);
          resolve();
          return;
        }
        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for mutation EventSource"));
        }
      }, 10);
    });

    const source = window.__eventSources[0];
    source.onmessage({
      data: `
        <mutation heimdall-content-target="#side" scope="self">
          <mutation-attr name="aria-live" value="polite"></mutation-attr>
          <mutation-class remove="pending"></mutation-class>
          <mutation-class add="ready"></mutation-class>
        </mutation>
        <span id="sse-mutation-main">Updated</span>
      `,
      lastEventId: "mutation-1"
    });

    return {
      url: source.url,
      sameSide: side === document.querySelector("#side"),
      identity: side.__identity,
      childPreserved: !!document.querySelector("#side-child"),
      sideClass: side.className,
      ariaLive: side.getAttribute("aria-live"),
      mainApplied: !!document.querySelector("#sse-mutation-main"),
      directives: document.querySelectorAll("mutation, mutation-attr, mutation-class").length,
      mutationEvents
    };
  });

  const url = new URL(state.url);
  assert.equal(url.searchParams.get("topic"), "topic:mutation");
  assert.equal(url.searchParams.get("st"), "st-sse-mutation");
  assert.equal(state.sameSide, true);
  assert.equal(state.identity, "preserved");
  assert.equal(state.childPreserved, true);
  assert.equal(state.sideClass, "ready");
  assert.equal(state.ariaLive, "polite");
  assert.equal(state.mainApplied, true);
  assert.equal(state.directives, 0);
  assert.deepEqual(state.mutationEvents, [
    "before:sse:#side",
    "after:sse:1"
  ]);
}

async function testSseCustomEventAndDisconnect(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-sse-custom"],
    bifrostTokens: ["st-custom"]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <div id="sse-host"></div>
      <div id="target">Old</div>
    `;

    window.__eventSources = [];
    window.EventSource = class {
      constructor(url) {
        this.url = url;
        this.listeners = {};
        this.closed = false;
        window.__eventSources.push(this);
      }

      addEventListener(name, handler) {
        this.listeners[name] = handler;
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

    const handle = window.Heimdall.sse.connect("topic:custom", {
      element: document.querySelector("#sse-host"),
      target: "#target",
      swap: "inner",
      event: "custom-event"
    });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        const es = window.__eventSources[0];
        if (es && typeof es.listeners["custom-event"] === "function") {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for custom EventSource handler"));
        }
      }, 10);
    });

    const es = window.__eventSources[0];
    es.listeners["custom-event"]({
      data: '<strong id="custom-done">Custom</strong>',
      lastEventId: "custom-1"
    });

    handle.close();

    return {
      eventSourceUrl: es.url,
      closed: es.closed,
      targetHtml: document.querySelector("#target").innerHTML,
      handleTopic: handle.topic,
      handleUrl: handle.url,
      closeEvents
    };
  });

  const eventSourceUrl = new URL(state.eventSourceUrl);
  assert.equal(eventSourceUrl.searchParams.get("topic"), "topic:custom");
  assert.equal(eventSourceUrl.searchParams.get("st"), "st-custom");
  assert.equal(state.targetHtml, '<strong id="custom-done">Custom</strong>');
  assert.equal(state.handleTopic, "topic:custom");
  assert.equal(state.handleUrl, state.eventSourceUrl);
  assert.equal(state.closed, true);
  assert.deepEqual(state.closeEvents, [{ topic: "topic:custom", reason: "manual" }]);
}

async function testSseSharedConnectionAcrossEvents(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-sse-shared"],
    bifrostTokens: ["st-shared"]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <div id="sse-host-a"></div>
      <div id="sse-host-b"></div>
      <div id="target-a">A old</div>
      <div id="target-b">B old</div>
    `;

    window.__eventSources = [];
    window.EventSource = class {
      constructor(url) {
        this.url = url;
        this.listeners = {};
        this.closed = false;
        window.__eventSources.push(this);
      }

      addEventListener(name, handler) {
        this.listeners[name] = handler;
      }

      removeEventListener(name, handler) {
        if (this.listeners[name] === handler) {
          delete this.listeners[name];
        }
      }

      close() {
        this.closed = true;
      }
    };

    const messages = [];
    const closeEvents = [];

    document.addEventListener("heimdall:sse-message", ev => {
      messages.push({
        topic: ev.detail.topic,
        event: ev.detail.event,
        targetId: ev.detail.el && ev.detail.el.id
      });
    });

    document.addEventListener("heimdall:sse-close", ev => {
      closeEvents.push({
        topic: ev.detail.topic,
        reason: ev.detail.reason,
        el: ev.detail.el && ev.detail.el.id
      });
    });

    const first = window.Heimdall.sse.connect("topic:shared", {
      element: document.querySelector("#sse-host-a"),
      target: "#target-a",
      swap: "inner",
      event: "order.updated"
    });

    const second = window.Heimdall.sse.connect("topic:shared", {
      element: document.querySelector("#sse-host-b"),
      target: "#target-b",
      swap: "inner",
      event: "toast"
    });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        const es = window.__eventSources[0];
        if (
          window.__eventSources.length === 1 &&
          es &&
          typeof es.listeners["order.updated"] === "function" &&
          typeof es.listeners.toast === "function"
        ) {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for shared EventSource listeners"));
        }
      }, 10);
    });

    const es = window.__eventSources[0];
    es.listeners["order.updated"]({
      data: '<span id="order-done">Order</span>',
      lastEventId: "order-1"
    });

    first.close();
    const closedAfterFirst = es.closed;

    es.listeners.toast({
      data: '<strong id="toast-done">Toast</strong>',
      lastEventId: "toast-1"
    });

    second.close();

    return {
      eventSourceCount: window.__eventSources.length,
      eventSourceUrl: es.url,
      firstUrl: first.url,
      secondUrl: second.url,
      closedAfterFirst,
      closedAfterSecond: es.closed,
      targetA: document.querySelector("#target-a").innerHTML,
      targetB: document.querySelector("#target-b").innerHTML,
      messages,
      closeEvents
    };
  });

  const eventSourceUrl = new URL(state.eventSourceUrl);

  assert.equal(state.eventSourceCount, 1);
  assert.equal(eventSourceUrl.searchParams.get("topic"), "topic:shared");
  assert.equal(eventSourceUrl.searchParams.get("st"), "st-shared");
  assert.equal(state.firstUrl, state.eventSourceUrl);
  assert.equal(state.secondUrl, state.eventSourceUrl);
  assert.equal(state.closedAfterFirst, false);
  assert.equal(state.closedAfterSecond, true);
  assert.equal(state.targetA, '<span id="order-done">Order</span>');
  assert.equal(state.targetB, '<strong id="toast-done">Toast</strong>');
  assert.deepEqual(state.messages, [
    { topic: "topic:shared", event: "order.updated", targetId: "sse-host-a" },
    { topic: "topic:shared", event: "toast", targetId: "sse-host-b" }
  ]);
  assert.deepEqual(state.closeEvents, [
    { topic: "topic:shared", reason: "manual", el: "sse-host-a" },
    { topic: "topic:shared", reason: "manual", el: "sse-host-b" }
  ]);

  const fetches = await getFetches(page);
  const tokenFetches = fetches.filter(fetch => fetch.url.includes("/__heimdall/v1/bifrost/token"));
  assert.equal(tokenFetches.length, 1);
}

export const tests = [
  ["applies SSE messages and emits message events", testSseMessageSwap],
  ["applies SSE mutations while preserving the configured main swap", testSseMutation],
  ["handles custom SSE events and disconnects", testSseCustomEventAndDisconnect],
  ["shares SSE connections across event subscribers", testSseSharedConnectionAcrossEvents]
];
