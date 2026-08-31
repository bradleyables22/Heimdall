import assert from "node:assert/strict";
import {
  actionFetches,
  csrfFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";

async function testSseSubscribeToken(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-sse"],
    bifrostTokens: ["st-123"]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="sse-host"></div>';
    window.__eventSourceUrls = [];

    window.EventSource = class {
      constructor(url) {
        this.url = url;
        window.__eventSourceUrls.push(url);
      }

      addEventListener() {
      }

      close() {
      }
    };

    window.Heimdall.sse.connect("topic:one", {
      element: document.querySelector("#sse-host")
    });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        if (window.__eventSourceUrls.length > 0) {
          clearInterval(timer);
          resolve();
          return;
        }

        if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for EventSource"));
        }
      }, 10);
    });

    return {
      eventSourceUrl: window.__eventSourceUrls[0]
    };
  });

  const eventSourceUrl = new URL(state.eventSourceUrl);
  assert.equal(eventSourceUrl.pathname, "/__heimdall/v1/bifrost");
  assert.equal(eventSourceUrl.searchParams.get("topic"), "topic:one");
  assert.equal(eventSourceUrl.searchParams.get("st"), "st-123");

  const fetches = await getFetches(page);
  const tokenFetch = fetches.find(fetch => fetch.url.includes("/__heimdall/v1/bifrost/token"));

  assert.equal(csrfFetches(fetches).length, 1);
  assert.ok(tokenFetch);
  assert.equal(new URL(tokenFetch.url).searchParams.get("topic"), "topic:one");
  assert.equal(tokenFetch.headers.requestverificationtoken, "csrf-sse");
}

async function testSseTokenFetchFollowedAuthRedirect(page) {
  const serverSignInUrl = "http://heimdall.test/signin?ReturnUrl=%2F__heimdall%2Fv1%2Fbifrost%2Ftoken";
  const expectedSignInUrl = "http://heimdall.test/signin?ReturnUrl=%2F";

  await installFakeServer(page, {
    csrfTokens: ["csrf-sse-auth"],
    bifrostTokenResponses: [{
      body: '<form id="login-form">Sign in</form>',
      redirected: true,
      url: serverSignInUrl
    }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = '<div id="sse-host">Secure SSE</div>';
    localStorage.removeItem("heimdall-sse-auth-redirect");
    localStorage.removeItem("heimdall-sse-auth-event-source");

    window.EventSource = class {
      constructor(url) {
        localStorage.setItem("heimdall-sse-auth-event-source", url);
      }

      addEventListener() {
      }

      close() {
      }
    };

    document.addEventListener("heimdall:sse-redirect", event => {
      const detail = event.detail || {};
      localStorage.setItem("heimdall-sse-auth-redirect", JSON.stringify({
        topic: detail.topic || "",
        url: detail.url || "",
        status: detail.status || 0,
        redirectUrl: detail.redirectUrl || ""
      }));
    });

    window.Heimdall.sse.connect("topic:secure", {
      element: document.querySelector("#sse-host"),
      event: "message"
    });
  });

  await page.waitForURL(expectedSignInUrl);

  const state = await page.evaluate(() => ({
    redirect: JSON.parse(localStorage.getItem("heimdall-sse-auth-redirect") || "null"),
    eventSourceUrl: localStorage.getItem("heimdall-sse-auth-event-source")
  }));

  assert.equal(page.url(), expectedSignInUrl);
  assert.equal(state.eventSourceUrl, null);
  assert.deepEqual(state.redirect, {
    topic: "topic:secure",
    url: "http://heimdall.test/__heimdall/v1/bifrost/token?topic=topic%3Asecure",
    status: 200,
    redirectUrl: expectedSignInUrl
  });
}

async function testSseTokenAuthChallengeLocation(page) {
  const serverSignInUrl = "http://heimdall.test/signin?ReturnUrl=%2F__heimdall%2Fv1%2Fbifrost%2Ftoken";
  const expectedSignInUrl = "http://heimdall.test/signin?ReturnUrl=%2F";

  await installFakeServer(page, {
    csrfTokens: ["csrf-sse-auth-location"],
    bifrostTokenResponses: [{
      status: 401,
      body: "",
      location: serverSignInUrl
    }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = '<div id="sse-host">Secure SSE</div>';
    localStorage.removeItem("heimdall-sse-auth-location-redirect");
    localStorage.removeItem("heimdall-sse-auth-location-event-source");

    window.EventSource = class {
      constructor(url) {
        localStorage.setItem("heimdall-sse-auth-location-event-source", url);
      }

      addEventListener() {
      }

      close() {
      }
    };

    document.addEventListener("heimdall:sse-redirect", event => {
      const detail = event.detail || {};
      localStorage.setItem("heimdall-sse-auth-location-redirect", JSON.stringify({
        topic: detail.topic || "",
        status: detail.status || 0,
        redirectUrl: detail.redirectUrl || ""
      }));
    });

    window.Heimdall.sse.connect("topic:secure-location", {
      element: document.querySelector("#sse-host"),
      event: "message"
    });
  });

  await page.waitForURL(expectedSignInUrl);

  const state = await page.evaluate(() => ({
    redirect: JSON.parse(localStorage.getItem("heimdall-sse-auth-location-redirect") || "null"),
    eventSourceUrl: localStorage.getItem("heimdall-sse-auth-location-event-source")
  }));

  assert.equal(page.url(), expectedSignInUrl);
  assert.equal(state.eventSourceUrl, null);
  assert.deepEqual(state.redirect, {
    topic: "topic:secure-location",
    status: 401,
    redirectUrl: expectedSignInUrl
  });
}

async function testSseTokenAntiforgeryRetry(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-sse-old", "csrf-sse-new"],
    bifrostTokenResponses: [
      { status: 400, body: "Invalid Heimdall antiforgery token." },
      { token: "st-sse-fresh" }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="sse-host"></div>';
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
        status: ev.detail.status
      });
    });

    window.Heimdall.sse.connect("topic:csrf-retry", {
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
          reject(new Error("Timed out waiting for CSRF retry EventSource"));
        }
      }, 10);
    });

    return {
      eventSourceUrl: window.__eventSources[0].url,
      scheduled
    };
  });

  const eventSourceUrl = new URL(state.eventSourceUrl);
  assert.equal(eventSourceUrl.searchParams.get("topic"), "topic:csrf-retry");
  assert.equal(eventSourceUrl.searchParams.get("st"), "st-sse-fresh");
  assert.deepEqual(state.scheduled, []);

  const fetches = await getFetches(page);
  const tokenFetches = fetches.filter(fetch => fetch.url.includes("/__heimdall/v1/bifrost/token"));

  assert.equal(csrfFetches(fetches).length, 2);
  assert.equal(tokenFetches.length, 2);
  assert.equal(tokenFetches[0].headers.requestverificationtoken, "csrf-sse-old");
  assert.equal(tokenFetches[1].headers.requestverificationtoken, "csrf-sse-new");
}

export const tests = [
  ["mints SSE subscribe tokens with CSRF", testSseSubscribeToken],
  ["navigates on fetch-followed SSE token auth redirects", testSseTokenFetchFollowedAuthRedirect],
  ["navigates on SSE token auth challenge locations", testSseTokenAuthChallengeLocation],
  ["retries SSE token minting after suspected CSRF failure", testSseTokenAntiforgeryRetry]
];
