import assert from "node:assert/strict";
import {
  actionFetches,
  csrfFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";
import { emulateTimezone } from "../helpers/timezone.mjs";

async function testDuplicateTimeLocalizationState(page) {
  await emulateTimezone(page, "America/New_York");

  const initial = await page.evaluate(() => {
    document.documentElement.lang = "en-US";
    document.body.innerHTML = `
      <span id="first" heimdall-time="2026-08-26T18:30:05.123Z" heimdall-time-format="HH:mm:ss"></span>
      <span id="second" heimdall-time="2026-08-26T18:30:05.123Z" heimdall-time-format="HH:mm:ss"></span>
    `;
    window.__timeBeforeIds = [];
    document.addEventListener("heimdall:time-before", event => {
      window.__timeBeforeIds.push(event.detail.element.id);
    });

    window.Heimdall.boot(document);
    window.Heimdall.boot(document);

    return {
      first: document.querySelector("#first").textContent,
      second: document.querySelector("#second").textContent,
      beforeIds: [...window.__timeBeforeIds]
    };
  });

  assert.deepEqual(initial, {
    first: "14:30:05",
    second: "14:30:05",
    beforeIds: ["first", "second"]
  });

  await page.evaluate(() => {
    document.querySelector("#first").setAttribute("heimdall-time-format", "HH:mm");
  });
  await page.waitForFunction(() => document.querySelector("#first").textContent === "14:30");

  const changed = await page.evaluate(() => ({
    first: document.querySelector("#first").textContent,
    second: document.querySelector("#second").textContent,
    beforeIds: [...window.__timeBeforeIds]
  }));

  assert.deepEqual(changed, {
    first: "14:30",
    second: "14:30:05",
    beforeIds: ["first", "second", "first"]
  });
}

async function testTimeLocalizationBeforeSwap(page) {
  await emulateTimezone(page, "America/New_York");
  await installFakeServer(page, {
    actionResponses: [{
      body: `<span id="swapped-time"
                   heimdall-time="2026-08-26T18:30:05.123Z"
                   heimdall-time-format="MMMM d, yyyy HH:mm">server fallback</span>`
    }]
  });

  const state = await page.evaluate(async () => {
    document.documentElement.lang = "en-US";
    document.body.innerHTML = '<div id="target" lang="fr-FR">Old</div>';
    const events = [];

    document.addEventListener("heimdall:swap-before", event => {
      events.push({
        name: "swap-before",
        text: event.detail.fragment.textContent.trim()
      });
    });
    document.addEventListener("heimdall:time-before", event => {
      events.push({
        name: "time-before",
        connected: event.detail.element.isConnected,
        locale: event.detail.locale
      });
    });
    document.addEventListener("heimdall:time-after", event => {
      events.push({
        name: "time-after",
        connected: event.detail.element.isConnected,
        text: event.detail.text
      });
    });
    document.addEventListener("heimdall:swap-after", event => {
      events.push({
        name: "swap-after",
        text: event.detail.target.textContent.trim()
      });
    });

    const result = await window.Heimdall.invoke("Time.Swap", {}, { target: "#target" });
    return {
      ok: result.ok,
      text: document.querySelector("#swapped-time").textContent,
      events
    };
  });

  assert.equal(state.ok, true);
  assert.equal(state.text, "août 26, 2026 14:30");
  assert.deepEqual(state.events, [
    { name: "swap-before", text: "server fallback" },
    { name: "time-before", connected: false, locale: "fr-FR" },
    { name: "time-after", connected: false, text: "août 26, 2026 14:30" },
    { name: "swap-after", text: "août 26, 2026 14:30" }
  ]);
}

async function testTimeLocalizationAllSwapModes(page) {
  await emulateTimezone(page, "America/New_York");
  const timeMarkup = (id, tag = "span", extra = "") =>
    `<${tag} id="${id}" ${extra}
             heimdall-time="2026-08-26T18:30:05.123Z"
             heimdall-time-format="HH:mm:ss.fff zzz">fallback</${tag}>`;

  await installFakeServer(page, {
    actionResponses: [
      { body: timeMarkup("time-inner") },
      { body: timeMarkup("target-outer", "div") },
      { body: timeMarkup("time-beforeend") },
      { body: timeMarkup("time-afterbegin") }
    ]
  });

  const state = await page.evaluate(async () => {
    document.documentElement.lang = "en-US";
    document.body.innerHTML = `
      <div id="target-inner">inner original</div>
      <div id="target-outer">outer original</div>
      <div id="target-beforeend"><span>beforeend original</span></div>
      <div id="target-afterbegin"><span>afterbegin original</span></div>
    `;
    const localized = [];
    document.addEventListener("heimdall:time-after", event => {
      if (event.detail.element.id.startsWith("time-") || event.detail.element.id === "target-outer") {
        localized.push({
          id: event.detail.element.id,
          connected: event.detail.element.isConnected,
          kind: event.detail.kind,
          origin: event.detail.origin
        });
      }
    });

    await window.Heimdall.invoke("Time.Inner", {}, { target: "#target-inner", swap: "inner" });
    await window.Heimdall.invoke("Time.Outer", {}, { target: "#target-outer", swap: "outer" });
    await window.Heimdall.invoke("Time.BeforeEnd", {}, { target: "#target-beforeend", swap: "beforeend" });
    await window.Heimdall.invoke("Time.AfterBegin", {}, { target: "#target-afterbegin", swap: "afterbegin" });

    return {
      inner: document.querySelector("#time-inner").textContent,
      outer: document.querySelector("#target-outer").textContent,
      beforeend: document.querySelector("#time-beforeend").textContent,
      afterbegin: document.querySelector("#time-afterbegin").textContent,
      beforeendLast: document.querySelector("#target-beforeend").lastElementChild.id,
      afterbeginFirst: document.querySelector("#target-afterbegin").firstElementChild.id,
      localized
    };
  });

  assert.deepEqual(state, {
    inner: "14:30:05.123 -04:00",
    outer: "14:30:05.123 -04:00",
    beforeend: "14:30:05.123 -04:00",
    afterbegin: "14:30:05.123 -04:00",
    beforeendLast: "time-beforeend",
    afterbeginFirst: "time-afterbegin",
    localized: [
      { id: "time-inner", connected: false, kind: "main", origin: "action" },
      { id: "target-outer", connected: false, kind: "main", origin: "action" },
      { id: "time-beforeend", connected: false, kind: "main", origin: "action" },
      { id: "time-afterbegin", connected: false, kind: "main", origin: "action" }
    ]
  });
}

async function testTimeLocalizationOutOfBand(page) {
  await emulateTimezone(page, "America/New_York");
  await installFakeServer(page, {
    actionResponses: [{
      body: `
        <invocation heimdall-content-target="#time-side">
          <template><span id="time-oob"
                          heimdall-time="2026-08-26T18:30:05.123Z"
                          heimdall-time-format="MMMM d HH:mm">side fallback</span></template>
        </invocation>
        <span id="time-main"
              heimdall-time="2026-08-26T18:30:05.123Z"
              heimdall-time-format="MMMM d HH:mm">main fallback</span>`
    }]
  });

  const state = await page.evaluate(async () => {
    document.documentElement.lang = "en-US";
    document.body.innerHTML = `
      <div id="time-target" lang="en-US">main original</div>
      <div id="time-side" lang="fr-FR">side original</div>
    `;
    const events = [];
    document.addEventListener("heimdall:time-after", event => {
      if (event.detail.element.id === "time-main" || event.detail.element.id === "time-oob") {
        events.push({
          id: event.detail.element.id,
          locale: event.detail.locale,
          connected: event.detail.element.isConnected,
          kind: event.detail.kind
        });
      }
    });

    await window.Heimdall.invoke("Time.Oob", {}, { target: "#time-target" });
    return {
      main: document.querySelector("#time-main").textContent,
      side: document.querySelector("#time-oob").textContent,
      events
    };
  });

  assert.deepEqual(state, {
    main: "August 26 14:30",
    side: "août 26 14:30",
    events: [
      { id: "time-oob", locale: "fr-FR", connected: false, kind: "invocation" },
      { id: "time-main", locale: "en-US", connected: false, kind: "main" }
    ]
  });
}

async function testTimeLocalizationSse(page) {
  await emulateTimezone(page, "America/New_York");
  await installFakeServer(page, {
    csrfTokens: ["csrf-time-sse"],
    bifrostTokens: ["st-time-sse"]
  });

  const state = await page.evaluate(async () => {
    document.documentElement.lang = "en-US";
    document.body.innerHTML = `
      <div id="time-sse-host"></div>
      <div id="time-sse-main" lang="en-US">main original</div>
      <div id="time-sse-side" lang="fr-FR">side original</div>
    `;
    window.__timeEventSources = [];
    window.EventSource = class {
      constructor(url) {
        this.url = url;
        this.listeners = {};
        window.__timeEventSources.push(this);
      }
      addEventListener(name, handler) {
        this.listeners[name] = handler;
      }
      close() {
        this.closed = true;
      }
    };

    const events = [];
    document.addEventListener("heimdall:time-after", event => {
      if (event.detail.origin === "sse") {
        events.push({
          id: event.detail.element.id,
          locale: event.detail.locale,
          connected: event.detail.element.isConnected,
          kind: event.detail.kind
        });
      }
    });

    window.Heimdall.sse.connect("topic:time", {
      element: document.querySelector("#time-sse-host"),
      target: "#time-sse-main",
      swap: "inner",
      event: "message"
    });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        const source = window.__timeEventSources[0];
        if (source && typeof source.onmessage === "function") {
          clearInterval(timer);
          resolve();
          return;
        }
        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for local-time EventSource."));
        }
      }, 10);
    });

    const source = window.__timeEventSources[0];
    source.onmessage({
      data: `
        <invocation heimdall-content-target="#time-sse-side">
          <template><span id="time-sse-oob"
                          heimdall-time="2026-08-26T18:30:05.123Z"
                          heimdall-time-format="MMMM d HH:mm">side fallback</span></template>
        </invocation>
        <span id="time-sse-result"
              heimdall-time="2026-08-26T18:30:05.123Z"
              heimdall-time-format="MMMM d HH:mm">main fallback</span>`,
      lastEventId: "time-1"
    });

    return {
      main: document.querySelector("#time-sse-result").textContent,
      side: document.querySelector("#time-sse-oob").textContent,
      events
    };
  });

  assert.deepEqual(state, {
    main: "August 26 14:30",
    side: "août 26 14:30",
    events: [
      { id: "time-sse-oob", locale: "fr-FR", connected: false, kind: "invocation" },
      { id: "time-sse-result", locale: "en-US", connected: false, kind: "main" }
    ]
  });
}

async function testTimeLocalizationEvents(page) {
  await emulateTimezone(page, "America/New_York");

  const state = await page.evaluate(() => {
    document.documentElement.lang = "en-US";
    document.body.innerHTML = `
      <span id="custom" heimdall-time="2026-08-26T18:30:05.123Z" heimdall-time-format="g">fallback custom</span>
      <span id="cancelled" heimdall-time="2026-08-26T18:30:05.123Z" heimdall-time-format="g">fallback cancelled</span>
      <span id="invalid" heimdall-time="not-a-time" heimdall-time-format="g">fallback invalid</span>
    `;

    const after = [];
    const errors = [];
    document.addEventListener("heimdall:time-before", event => {
      if (event.detail.element.id === "custom")
        event.detail.text = "<b>custom local time</b>";
      if (event.detail.element.id === "cancelled")
        event.preventDefault();
    });
    document.addEventListener("heimdall:time-after", event => {
      after.push({ id: event.detail.element.id, text: event.detail.text });
    });
    document.addEventListener("heimdall:time-error", event => {
      errors.push({ id: event.detail.element.id, name: event.detail.error.name });
    });

    window.Heimdall.boot(document);
    window.Heimdall.boot(document);

    return {
      customText: document.querySelector("#custom").textContent,
      customHtml: document.querySelector("#custom").innerHTML,
      cancelledText: document.querySelector("#cancelled").textContent,
      invalidText: document.querySelector("#invalid").textContent,
      after,
      errors
    };
  });

  assert.deepEqual(state, {
    customText: "<b>custom local time</b>",
    customHtml: "&lt;b&gt;custom local time&lt;/b&gt;",
    cancelledText: "fallback cancelled",
    invalidText: "fallback invalid",
    after: [{ id: "custom", text: "<b>custom local time</b>" }],
    errors: [{ id: "invalid", name: "RangeError" }]
  });
}

async function testTimeLocalizationMutationObserver(page) {
  await emulateTimezone(page, "America/New_York");

  await page.evaluate(() => {
    document.documentElement.lang = "en-US";
    document.body.innerHTML = `
      <div id="language-host" lang="en-US">
        <span id="language-time"
              heimdall-time="2026-08-26T18:30:05.123Z"
              heimdall-time-format="MMMM"></span>
      </div>
      <span id="document-language-time"
            heimdall-time="2026-08-26T18:30:05.123Z"
            heimdall-time-format="MMMM"></span>
      <div id="dynamic-time-host"></div>
    `;
    window.Heimdall.boot(document);
  });

  assert.equal(await page.locator("#language-time").textContent(), "August");

  await page.evaluate(() => {
    document.querySelector("#language-host").setAttribute("lang", "fr-FR");
  });
  await page.waitForFunction(() => document.querySelector("#language-time").textContent === "août");

  await page.evaluate(() => {
    document.documentElement.lang = "fr-FR";
  });
  await page.waitForFunction(() => document.querySelector("#document-language-time").textContent === "août");

  await page.evaluate(() => {
    document.querySelector("#dynamic-time-host").insertAdjacentHTML(
      "beforeend",
      '<span id="dynamic-time" heimdall-time="2026-08-26T18:30:05.123Z" heimdall-time-format="HH:mm">fallback</span>'
    );
  });
  await page.waitForFunction(() => document.querySelector("#dynamic-time").textContent === "14:30");

  assert.equal(await page.locator("#language-time").textContent(), "août");
  assert.equal(await page.locator("#document-language-time").textContent(), "août");
  assert.equal(await page.locator("#dynamic-time").textContent(), "14:30");
}

export const tests = [
  ["tracks duplicate time elements independently and reprocesses changed inputs", testDuplicateTimeLocalizationState],
  ["localizes action fragments before they enter the DOM", testTimeLocalizationBeforeSwap],
  ["localizes every action swap mode before insertion", testTimeLocalizationAllSwapModes],
  ["localizes main and out-of-band action fragments in their target languages", testTimeLocalizationOutOfBand],
  ["localizes main and out-of-band SSE fragments before insertion", testTimeLocalizationSse],
  ["supports cancellable, mutable, and error time lifecycle events", testTimeLocalizationEvents],
  ["relocalizes inherited-language changes and observed DOM additions", testTimeLocalizationMutationObserver]
];
