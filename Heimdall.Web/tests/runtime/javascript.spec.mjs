import assert from "node:assert/strict";
import {
  actionFetches,
  csrfFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";

async function testJsInvokeVoidAfterSwap(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: `
        <javascript function="window.App.record" args='["after","#updated"]'></javascript>
        <span id="updated">Updated</span>
      `
    }]
  });

  const state = await page.evaluate(async () => {
    window.__jsCalls = [];
    window.App = {
      record(label, selector) {
        const el = document.querySelector(selector);
        window.__jsCalls.push({
          label,
          exists: !!el,
          targetHtml: document.querySelector("#target").innerHTML
        });
      }
    };

    document.body.innerHTML = '<div id="target">Old</div>';
    await window.Heimdall.invoke("Js.After", {}, { target: "#target" });

    return {
      targetHtml: document.querySelector("#target").innerHTML,
      calls: window.__jsCalls,
      directiveCount: document.querySelectorAll("javascript").length
    };
  });

  assert.equal(state.targetHtml, '\n        \n        <span id="updated">Updated</span>\n      ');
  assert.deepEqual(state.calls, [{
    label: "after",
    exists: true,
    targetHtml: '\n        \n        <span id="updated">Updated</span>\n      '
  }]);
  assert.equal(state.directiveCount, 0);
}

async function testJsInvokeVoidBeforeSwap(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: `
        <javascript function="window.App.record" timing="before" args='["before","#updated"]'></javascript>
        <span id="updated">Updated</span>
      `
    }]
  });

  const state = await page.evaluate(async () => {
    window.__jsCalls = [];
    window.App = {
      record(label, selector) {
        window.__jsCalls.push({
          label,
          exists: !!document.querySelector(selector),
          targetHtml: document.querySelector("#target").innerHTML
        });
      }
    };

    document.body.innerHTML = '<div id="target">Old</div>';
    await window.Heimdall.invoke("Js.Before", {}, { target: "#target" });

    return {
      targetHtml: document.querySelector("#target").innerHTML,
      calls: window.__jsCalls,
      directiveCount: document.querySelectorAll("javascript").length
    };
  });

  assert.equal(state.targetHtml, '\n        \n        <span id="updated">Updated</span>\n      ');
  assert.deepEqual(state.calls, [{
    label: "before",
    exists: false,
    targetHtml: "Old"
  }]);
  assert.equal(state.directiveCount, 0);
}

async function testJsInvokeVoidInvalidPath(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: '<javascript function="App.record" args=\'["bad"]\'></javascript><span id="done">Done</span>'
    }]
  });

  const state = await page.evaluate(async () => {
    const errors = [];
    document.addEventListener("heimdall:javascript-error", ev => {
      errors.push({
        functionPath: ev.detail.functionPath,
        timing: ev.detail.timing,
        phase: ev.detail.context && ev.detail.context.phase,
        message: ev.detail.error && ev.detail.error.message
      });
    });

    window.App = {
      record() {
        window.__invalidPathRan = true;
      }
    };

    document.body.innerHTML = '<div id="target">Old</div>';
    await window.Heimdall.invoke("Js.Invalid", {}, { target: "#target" });

    return {
      targetHtml: document.querySelector("#target").innerHTML,
      ran: window.__invalidPathRan === true,
      errors,
      directiveCount: document.querySelectorAll("javascript").length
    };
  });

  assert.equal(state.targetHtml, '<span id="done">Done</span>');
  assert.equal(state.ran, false);
  assert.equal(state.directiveCount, 0);
  assert.equal(state.errors.length, 1);
  assert.equal(state.errors[0].functionPath, "App.record");
  assert.equal(state.errors[0].timing, "after");
  assert.equal(state.errors[0].phase, "after");
  assert.ok(state.errors[0].message.includes("must start"));
}

async function testJsInvokeVoidRedirectHardStop(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: '<javascript function="window.App.record" args=\'["redirect"]\'></javascript><redirect url="#js-redirected"></redirect><span>Ignored</span>'
    }]
  });

  const result = await page.evaluate(() => {
    window.__jsCalls = [];
    window.App = {
      record(label) {
        window.__jsCalls.push(label);
      }
    };

    document.body.innerHTML = '<div id="main">Keep</div>';
    return window.Heimdall.invoke("Js.Redirect", {}, { target: "#main" });
  });

  assert.equal(result.redirectUrl, "#js-redirected");
  assert.equal(await page.locator("#main").innerHTML(), "Keep");

  const calls = await page.evaluate(() => window.__jsCalls);
  assert.deepEqual(calls, []);
  assert.equal(new URL(page.url()).hash, "#js-redirected");
}

async function testJsInvokeVoidAbortResponse(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: `
        <abort reason="stay-put"></abort>
        <javascript function="window.App.record" args='["after-abort"]'></javascript>
        <span id="should-not-apply">Nope</span>
      `
    }]
  });

  const state = await page.evaluate(async () => {
    window.__jsCalls = [];
    window.App = {
      record(label) {
        window.__jsCalls.push({
          label,
          targetHtml: document.querySelector("#main").innerHTML
        });
      }
    };

    document.body.innerHTML = '<div id="main">Keep</div>';
    const result = await window.Heimdall.invoke("Js.Abort", {}, { target: "#main" });

    return {
      result,
      calls: window.__jsCalls,
      targetHtml: document.querySelector("#main").innerHTML
    };
  });

  assert.equal(state.result.abortSwap, true);
  assert.equal(state.result.abortReason, "stay-put");
  assert.equal(state.targetHtml, "Keep");
  assert.deepEqual(state.calls, [{
    label: "after-abort",
    targetHtml: "Keep"
  }]);
}

async function testJsInvokeVoidThisBinding(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: '<javascript function="window.App.counter.add" args=\'[3]\'></javascript><span id="done">Done</span>'
    }]
  });

  const state = await page.evaluate(async () => {
    window.App = {
      counter: {
        value: 4,
        add(amount) {
          this.value += amount;
        }
      }
    };

    document.body.innerHTML = '<div id="target">Old</div>';
    await window.Heimdall.invoke("Js.This", {}, { target: "#target" });

    return {
      value: window.App.counter.value,
      targetHtml: document.querySelector("#target").innerHTML
    };
  });

  assert.equal(state.value, 7);
  assert.equal(state.targetHtml, '<span id="done">Done</span>');
}

async function testJsInvokeVoidSseMessage(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-js-sse"],
    bifrostTokens: ["st-js-sse"]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <div id="sse-host"></div>
      <div id="target">Old</div>
    `;

    window.__jsCalls = [];
    window.App = {
      record(selector) {
        window.__jsCalls.push({
          selector,
          exists: !!document.querySelector(selector),
          targetHtml: document.querySelector("#target").innerHTML
        });
      }
    };

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

    window.Heimdall.sse.connect("topic:js", {
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
      data: '<javascript function="window.App.record" args=\'["#sse-done"]\'></javascript><span id="sse-done">SSE</span>',
      lastEventId: "js-1"
    });

    return {
      eventSourceUrl: es.url,
      targetHtml: document.querySelector("#target").innerHTML,
      calls: window.__jsCalls,
      directiveCount: document.querySelectorAll("javascript").length
    };
  });

  const eventSourceUrl = new URL(state.eventSourceUrl);
  assert.equal(eventSourceUrl.searchParams.get("topic"), "topic:js");
  assert.equal(eventSourceUrl.searchParams.get("st"), "st-js-sse");
  assert.equal(state.targetHtml, '<span id="sse-done">SSE</span>');
  assert.deepEqual(state.calls, [{
    selector: "#sse-done",
    exists: true,
    targetHtml: '<span id="sse-done">SSE</span>'
  }]);
  assert.equal(state.directiveCount, 0);
}

export const tests = [
  ["invokes JavaScript void directives after swaps", testJsInvokeVoidAfterSwap],
  ["invokes JavaScript void directives before swaps", testJsInvokeVoidBeforeSwap],
  ["emits JavaScript errors for invalid directive paths", testJsInvokeVoidInvalidPath],
  ["does not invoke JavaScript directives when redirecting", testJsInvokeVoidRedirectHardStop],
  ["invokes JavaScript directives on abort responses", testJsInvokeVoidAbortResponse],
  ["preserves this when invoking JavaScript methods", testJsInvokeVoidThisBinding],
  ["invokes JavaScript void directives from SSE messages", testJsInvokeVoidSseMessage]
];
