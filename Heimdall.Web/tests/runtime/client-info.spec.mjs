import assert from "node:assert/strict";
import {
  actionFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";

async function installClientInfoEnvironment(page) {
  await page.evaluate(() => {
    const mediaMatches = {
      "(prefers-color-scheme: dark)": true,
      "(prefers-color-scheme: light)": false,
      "(prefers-reduced-motion: reduce)": true,
      "(prefers-contrast: more)": true,
      "(prefers-contrast: less)": false,
      "(prefers-contrast: custom)": false,
      "(forced-colors: active)": false,
      "(pointer: coarse)": true,
      "(pointer: fine)": false,
      "(hover: hover)": false
    };
    const media = new Map();

    window.matchMedia = query => {
      if (!media.has(query)) {
        const listeners = new Set();
        media.set(query, {
          matches: mediaMatches[query] === true,
          media: query,
          addEventListener: (name, listener) => {
            if (name === "change") listeners.add(listener);
          },
          removeEventListener: (name, listener) => {
            if (name === "change") listeners.delete(listener);
          },
          addListener: listener => listeners.add(listener),
          removeListener: listener => listeners.delete(listener)
        });
      }
      return media.get(query);
    };

    const viewportListeners = new Set();
    window.__clientInfoViewport = {
      width: 390.5,
      height: 844.25,
      addEventListener: (name, listener) => {
        if (name === "resize") viewportListeners.add(listener);
      }
    };
    Object.defineProperty(window, "visualViewport", {
      configurable: true,
      value: window.__clientInfoViewport
    });
    Object.defineProperty(window, "devicePixelRatio", { configurable: true, value: 3 });
    Object.defineProperty(window.screen, "width", { configurable: true, value: 390 });
    Object.defineProperty(window.screen, "height", { configurable: true, value: 844 });
    Object.defineProperty(navigator, "language", { configurable: true, value: "en-US" });
    Object.defineProperty(navigator, "languages", { configurable: true, value: ["en-US", "es"] });
    Object.defineProperty(navigator, "maxTouchPoints", { configurable: true, value: 5 });
    Object.defineProperty(navigator, "onLine", { configurable: true, value: false });
  });
}

async function testClientInfoDisabledByDefault(page) {
  await installFakeServer(page, { actionResponses: [{ body: "" }] });

  await page.evaluate(() => window.Heimdall.invoke("ClientInfo.Default", null, { swap: "none" }));

  const action = actionFetches(await getFetches(page))[0];
  assert.ok(action);
  assert.equal(action.headers["x-heimdall-client-info"], undefined);
}

async function testClientInfoSnapshot(page) {
  await installClientInfoEnvironment(page);
  await installFakeServer(page, { actionResponses: [{ body: "" }] });

  await page.evaluate(() => {
    window.Heimdall.config.clientInfo = true;
    return window.Heimdall.invoke("ClientInfo.Snapshot", null, { swap: "none" });
  });

  const action = actionFetches(await getFetches(page))[0];
  const header = action.headers["x-heimdall-client-info"];
  const info = JSON.parse(header);

  assert.ok(header.length < 1000);
  assert.equal(info.timeZone, "America/New_York");
  assert.ok(info.utcOffsetMinutes === -240 || info.utcOffsetMinutes === -300);
  assert.equal(info.locale, "en-US");
  assert.deepEqual(info.languages, ["en-US", "es"]);
  assert.equal(info.viewportWidth, 390.5);
  assert.equal(info.viewportHeight, 844.25);
  assert.equal(info.screenWidth, 390);
  assert.equal(info.screenHeight, 844);
  assert.equal(info.devicePixelRatio, 3);
  assert.equal(info.orientation, "portrait");
  assert.equal(info.deviceCategory, "mobile");
  assert.equal(info.colorScheme, "dark");
  assert.equal(info.prefersReducedMotion, true);
  assert.equal(info.prefersContrast, "more");
  assert.equal(info.forcedColors, false);
  assert.equal(info.touch, true);
  assert.equal(info.maxTouchPoints, 5);
  assert.equal(info.pointer, "coarse");
  assert.equal(info.hover, false);
  assert.equal(info.online, false);
}

async function testClientInfoCacheInvalidation(page) {
  await installClientInfoEnvironment(page);
  await installFakeServer(page, {
    actionResponses: [{ body: "" }, { body: "" }, { body: "" }]
  });

  await page.evaluate(async () => {
    window.Heimdall.config.clientInfo = true;
    await window.Heimdall.invoke("ClientInfo.First", null, { swap: "none" });
    window.__clientInfoViewport.width = 700;
    await window.Heimdall.invoke("ClientInfo.Cached", null, { swap: "none" });
    window.dispatchEvent(new Event("resize"));
    await window.Heimdall.invoke("ClientInfo.Invalidated", null, { swap: "none" });
  });

  const actions = actionFetches(await getFetches(page));
  const headers = actions.map(action => action.headers["x-heimdall-client-info"]);
  assert.equal(headers.length, 3);
  assert.equal(headers[1], headers[0]);
  assert.notEqual(headers[2], headers[1]);
  assert.equal(JSON.parse(headers[2]).viewportWidth, 700);
}

async function testClientInfoZeroMaxAge(page) {
  await installClientInfoEnvironment(page);
  await installFakeServer(page, {
    actionResponses: [{ body: "" }, { body: "" }]
  });

  await page.evaluate(async () => {
    window.Heimdall.config.clientInfo = true;
    window.Heimdall.config.clientInfoMaxAgeMs = 0;
    await window.Heimdall.invoke("ClientInfo.First", null, { swap: "none" });
    window.__clientInfoViewport.width = 720;
    await window.Heimdall.invoke("ClientInfo.Fresh", null, { swap: "none" });
  });

  const actions = actionFetches(await getFetches(page));
  assert.equal(JSON.parse(actions[0].headers["x-heimdall-client-info"]).viewportWidth, 390.5);
  assert.equal(JSON.parse(actions[1].headers["x-heimdall-client-info"]).viewportWidth, 720);
}

async function testClientInfoLifecycleCustomization(page) {
  await installClientInfoEnvironment(page);
  await installFakeServer(page, {
    actionResponses: Array.from({ length: 6 }, () => ({ body: "" }))
  });

  const events = await page.evaluate(async () => {
    window.Heimdall.config.clientInfo = true;
    const seen = [];

    document.addEventListener("heimdall:client-info-before", event => {
      seen.push({
        actionId: event.detail.actionId,
        requestId: event.detail.requestId,
        attempt: event.detail.attempt,
        sourceId: event.detail.sourceElement?.id || null
      });

      switch (event.detail.actionId) {
        case "ClientInfo.Mutated":
          event.detail.info.locale = "en-HEIMDALL";
          event.detail.info.languages.push("custom");
          break;
        case "ClientInfo.Replaced":
          event.detail.info = { locale: "replacement" };
          break;
        case "ClientInfo.Omitted":
          event.preventDefault();
          break;
        case "ClientInfo.Circular":
          event.detail.info.self = event.detail.info;
          break;
      }
    });

    document.addEventListener("heimdall:request-before", event => {
      if (event.detail.actionId !== "ClientInfo.RawOverride")
        return;

      const headers = event.detail.request.headers;
      const info = JSON.parse(headers["X-Heimdall-Client-Info"]);
      info.colorScheme = "request-before";
      headers["X-Heimdall-Client-Info"] = JSON.stringify(info);
    });

    for (const actionId of [
      "ClientInfo.Mutated",
      "ClientInfo.Replaced",
      "ClientInfo.Cached",
      "ClientInfo.Omitted",
      "ClientInfo.Circular",
      "ClientInfo.RawOverride"
    ]) {
      await window.Heimdall.invoke(actionId, null, { swap: "none" });
    }

    return seen;
  });

  const actions = actionFetches(await getFetches(page));
  const byAction = new Map(actions.map(action => [
    action.headers["x-heimdall-content-action"],
    action.headers["x-heimdall-client-info"]
  ]));
  const mutated = JSON.parse(byAction.get("ClientInfo.Mutated"));
  const replaced = JSON.parse(byAction.get("ClientInfo.Replaced"));
  const cached = JSON.parse(byAction.get("ClientInfo.Cached"));
  const rawOverride = JSON.parse(byAction.get("ClientInfo.RawOverride"));

  assert.equal(actions.length, 6);
  assert.equal(events.length, 6);
  assert.deepEqual(events.map(event => event.actionId), [
    "ClientInfo.Mutated",
    "ClientInfo.Replaced",
    "ClientInfo.Cached",
    "ClientInfo.Omitted",
    "ClientInfo.Circular",
    "ClientInfo.RawOverride"
  ]);
  assert.ok(events.every(event => event.requestId > 0 && event.attempt === 1));
  assert.equal(mutated.locale, "en-HEIMDALL");
  assert.deepEqual(mutated.languages, ["en-US", "es", "custom"]);
  assert.deepEqual(replaced, { locale: "replacement" });
  assert.equal(cached.locale, "en-US");
  assert.deepEqual(cached.languages, ["en-US", "es"]);
  assert.equal(byAction.get("ClientInfo.Omitted"), undefined);
  assert.equal(byAction.get("ClientInfo.Circular"), undefined);
  assert.equal(rawOverride.colorScheme, "request-before");
}

async function testClientInfoLifecycleOnAntiforgeryRetry(page) {
  await installClientInfoEnvironment(page);
  await installFakeServer(page, {
    csrfTokens: ["csrf-first", "csrf-second"],
    actionResponses: [
      { status: 400, body: "antiforgery validation failed" },
      { status: 200, body: "" }
    ]
  });

  const attempts = await page.evaluate(async () => {
    window.Heimdall.config.clientInfo = true;
    const seen = [];
    document.addEventListener("heimdall:client-info-before", event => {
      if (event.detail.actionId !== "ClientInfo.Retry")
        return;

      seen.push(event.detail.attempt);
      event.detail.info.locale = `attempt-${event.detail.attempt}`;
    });

    await window.Heimdall.invoke("ClientInfo.Retry", null, { swap: "none" });
    return seen;
  });

  const actions = actionFetches(await getFetches(page));
  assert.deepEqual(attempts, [1, 2]);
  assert.equal(actions.length, 2);
  assert.equal(JSON.parse(actions[0].headers["x-heimdall-client-info"]).locale, "attempt-1");
  assert.equal(JSON.parse(actions[1].headers["x-heimdall-client-info"]).locale, "attempt-2");
}

export const tests = [
  ["omits browser information by default", testClientInfoDisabledByDefault],
  [
    "sends the bounded browser information schema when enabled",
    testClientInfoSnapshot,
    { runtimePage: { timezoneId: "America/New_York" } }
  ],
  ["caches browser information until a relevant browser event", testClientInfoCacheInvalidation],
  ["refreshes browser information on every request when max age is zero", testClientInfoZeroMaxAge],
  ["customizes or omits client information through request lifecycle hooks", testClientInfoLifecycleCustomization],
  ["re-emits mutable client information for antiforgery retries", testClientInfoLifecycleOnAntiforgeryRetry]
];
