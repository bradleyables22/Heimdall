import assert from "node:assert/strict";
import {
  actionFetches,
  csrfFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";

async function testAsyncRequestHeadersAcrossHeimdallRequests(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-headers"],
    bifrostTokens: ["bifrost-headers"],
    actionResponses: [{ body: "" }]
  });

  const contexts = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="request-headers-sse"></div>';
    window.__requestHeaderContexts = [];
    window.__requestHeaderEventSources = [];
    window.EventSource = class {
      constructor(url) {
        window.__requestHeaderEventSources.push(url);
      }
      addEventListener() {
      }
      close() {
      }
    };

    window.Heimdall.config.requestHeaders = async context => {
      await Promise.resolve();
      window.__requestHeaderContexts.push({
        kind: context.kind,
        url: context.url,
        method: context.method,
        actionId: context.actionId,
        topic: context.topic,
        requestId: context.requestId,
        attempt: context.attempt,
        sourceId: context.sourceElement?.id || null,
        hasSignal: !!context.signal,
        baseAction: context.headers["X-Heimdall-Content-Action"] || null,
        baseCsrf: context.headers.RequestVerificationToken || null
      });
      context.headers["X-Provider-Mutated"] = context.kind;
      context.headers["X-Provider-Order"] = "mutated";
      return {
        Authorization: `Bearer ${context.kind}`,
        "X-Provider-Returned": context.kind,
        "x-provider-order": "returned"
      };
    };

    await window.Heimdall.invoke("RequestHeaders.Action", {}, {
      swap: "none",
      headers: {
        authorization: "Bearer explicit",
        "X-Explicit": "yes"
      }
    });

    window.Heimdall.sse.connect("request-headers-topic", {
      element: document.querySelector("#request-headers-sse")
    });

    await new Promise((resolve, reject) => {
      const started = Date.now();
      const timer = setInterval(() => {
        if (window.__requestHeaderEventSources.length > 0) {
          clearInterval(timer);
          resolve();
        } else if (Date.now() - started > 3000) {
          clearInterval(timer);
          reject(new Error("Timed out waiting for the request-header SSE connection."));
        }
      }, 10);
    });

    return window.__requestHeaderContexts;
  });

  const fetches = await getFetches(page);
  const csrf = csrfFetches(fetches)[0];
  const action = actionFetches(fetches)[0];
  const bifrost = fetches.find(fetch => fetch.url.includes("/__heimdall/v1/bifrost/token"));

  assert.deepEqual(contexts.map(context => context.kind), [
    "csrf-token",
    "content-action",
    "bifrost-token"
  ]);
  assert.equal(contexts[0].method, "GET");
  assert.equal(contexts[0].hasSignal, false);
  assert.equal(contexts[1].actionId, "RequestHeaders.Action");
  assert.ok(contexts[1].requestId > 0);
  assert.equal(contexts[1].attempt, 1);
  assert.equal(contexts[1].hasSignal, true);
  assert.equal(contexts[1].baseAction, "RequestHeaders.Action");
  assert.equal(contexts[1].baseCsrf, "csrf-headers");
  assert.equal(contexts[2].topic, "request-headers-topic");
  assert.equal(contexts[2].baseCsrf, "csrf-headers");

  assert.equal(csrf.headers.authorization, "Bearer csrf-token");
  assert.equal(csrf.headers["x-provider-mutated"], "csrf-token");
  assert.equal(csrf.headers["x-provider-returned"], "csrf-token");
  assert.equal(action.headers.authorization, "Bearer explicit");
  assert.equal(action.headers["x-explicit"], "yes");
  assert.equal(action.headers["x-provider-mutated"], "content-action");
  assert.equal(action.headers["x-provider-returned"], "content-action");
  assert.equal(action.headers["x-provider-order"], "returned");
  assert.equal(bifrost.headers.authorization, "Bearer bifrost-token");
  assert.equal(bifrost.headers["x-provider-mutated"], "bifrost-token");
  assert.equal(bifrost.headers["x-provider-returned"], "bifrost-token");
}

async function testQueuedRequestHeadersResolveAtExecution(page) {
  await installFakeServer(page, {
    actionResponses: [
      { delayMs: 80, body: "" },
      { body: "" }
    ]
  });

  await page.evaluate(async () => {
    window.Heimdall.config.antiforgery = false;
    window.__queuedHeaderToken = "queued-old";
    window.Heimdall.config.requestHeaders = context => ({
      Authorization: `Bearer ${window.__queuedHeaderToken}`,
      "X-Provider-Attempt": String(context.attempt)
    });

    const first = window.Heimdall.invoke("RequestHeaders.Queue.First", {}, {
      swap: "none",
      sync: "queue-latest",
      syncGroup: "request-headers-queue"
    });
    const second = window.Heimdall.invoke("RequestHeaders.Queue.Second", {}, {
      swap: "none",
      sync: "queue-latest",
      syncGroup: "request-headers-queue"
    });
    window.__queuedHeaderToken = "queued-fresh";

    await Promise.all([first, second]);
  });

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 2);
  assert.equal(actions[0].headers.authorization, "Bearer queued-old");
  assert.equal(actions[1].headers.authorization, "Bearer queued-fresh");
}

async function testRequestHeadersRerunForAntiforgeryRetry(page) {
  await installFakeServer(page, {
    csrfTokens: ["csrf-request-headers-old", "csrf-request-headers-new"],
    actionResponses: [
      { status: 400, body: "antiforgery validation failed" },
      { status: 200, body: "" }
    ]
  });

  const attempts = await page.evaluate(async () => {
    const contentAttempts = [];
    window.Heimdall.config.requestHeaders = context => {
      if (context.kind === "content-action")
        contentAttempts.push(context.attempt);
      return { Authorization: `Bearer ${context.kind}-${context.attempt}` };
    };

    const result = await window.Heimdall.invoke("RequestHeaders.Retry", {}, { swap: "none" });
    return { contentAttempts, ok: result.ok };
  });

  const fetches = await getFetches(page);
  const actions = actionFetches(fetches);
  assert.equal(attempts.ok, true);
  assert.deepEqual(attempts.contentAttempts, [1, 2]);
  assert.equal(csrfFetches(fetches).length, 2);
  assert.equal(actions.length, 2);
  assert.equal(actions[0].headers.authorization, "Bearer content-action-1");
  assert.equal(actions[1].headers.authorization, "Bearer content-action-2");
}

async function testRequestHeaderShapesAndLifecyclePrecedence(page) {
  await installFakeServer(page, {
    actionResponses: Array.from({ length: 4 }, () => ({ body: "" }))
  });

  const beforeSeen = await page.evaluate(async () => {
    window.Heimdall.config.antiforgery = false;
    window.Heimdall.config.requestHeaders = context => {
      switch (context.actionId) {
        case "RequestHeaders.Headers":
          return new Headers({ "X-Header-Shape": "headers" });
        case "RequestHeaders.Pairs":
          return [["X-Header-Shape", "pairs"]];
        case "RequestHeaders.Mutated":
          context.headers["X-Header-Shape"] = "mutated";
          return null;
        case "RequestHeaders.Precedence":
          return { "X-Header-Order": "provider" };
        default:
          return {};
      }
    };

    document.addEventListener("heimdall:request-config", event => {
      if (event.detail.actionId === "RequestHeaders.Precedence")
        event.detail.headers["x-header-order"] = "request-config";
    });

    let seen = null;
    document.addEventListener("heimdall:request-before", event => {
      if (event.detail.actionId !== "RequestHeaders.Precedence")
        return;

      const headers = event.detail.request.headers;
      const key = Object.keys(headers).find(name => name.toLowerCase() === "x-header-order");
      seen = headers[key];
      headers[key] = "request-before";
    });

    for (const actionId of [
      "RequestHeaders.Headers",
      "RequestHeaders.Pairs",
      "RequestHeaders.Mutated",
      "RequestHeaders.Precedence"
    ]) {
      await window.Heimdall.invoke(actionId, {}, { swap: "none" });
    }

    return seen;
  });

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 4);
  assert.equal(actions[0].headers["x-header-shape"], "headers");
  assert.equal(actions[1].headers["x-header-shape"], "pairs");
  assert.equal(actions[2].headers["x-header-shape"], "mutated");
  assert.equal(beforeSeen, "request-config");
  assert.equal(actions[3].headers["x-header-order"], "request-before");
}

async function testRequestHeadersPreserveMultipartRequests(page) {
  await installFakeServer(page, { actionResponses: [{ body: "" }] });

  const providerSawContentType = await page.evaluate(async () => {
    window.Heimdall.config.antiforgery = false;
    let contentType = "not-called";
    window.Heimdall.config.requestHeaders = context => {
      contentType = Object.keys(context.headers)
        .find(name => name.toLowerCase() === "content-type") || null;
      return { Authorization: "Bearer multipart" };
    };

    const form = new FormData();
    form.append("caption", "request headers multipart");
    form.append("attachment", new File(["payload"], "payload.txt", { type: "text/plain" }));
    await window.Heimdall.invoke("RequestHeaders.Multipart", form, { swap: "none" });
    return contentType;
  });

  const action = actionFetches(await getFetches(page))[0];
  assert.equal(providerSawContentType, null);
  assert.equal(action.headers.authorization, "Bearer multipart");
  assert.equal(action.headers["content-type"], undefined);
  assert.deepEqual(action.formBody, [
    { name: "caption", value: "request headers multipart" },
    {
      name: "attachment",
      value: { fileName: "payload.txt", size: 7, type: "text/plain" }
    }
  ]);
}

async function testPendingRequestHeadersRespectReplacementAndExternalAbort(page) {
  await installFakeServer(page, { actionResponses: [{ body: "" }] });

  const state = await page.evaluate(async () => {
    window.Heimdall.config.antiforgery = false;
    const calls = [];
    window.Heimdall.config.requestHeaders = context => {
      calls.push(context.actionId);
      if (context.actionId === "RequestHeaders.Replace.Pending" ||
          context.actionId === "RequestHeaders.External.Pending") {
        return new Promise(() => {});
      }
      return { Authorization: "Bearer replacement" };
    };

    const pending = window.Heimdall.invoke("RequestHeaders.Replace.Pending", {}, {
      swap: "none",
      sync: "replace",
      syncGroup: "request-header-replace"
    });
    const replacement = window.Heimdall.invoke("RequestHeaders.Replace.Winner", {}, {
      swap: "none",
      sync: "replace",
      syncGroup: "request-header-replace"
    });

    const controller = new AbortController();
    const external = window.Heimdall.invoke("RequestHeaders.External.Pending", {}, {
      swap: "none",
      signal: controller.signal
    });
    controller.abort();

    const [pendingResult, replacementResult, externalResult] = await Promise.all([
      pending,
      replacement,
      external
    ]);
    return { pendingResult, replacementResult, externalResult, calls };
  });

  const actions = actionFetches(await getFetches(page));
  assert.equal(state.pendingResult.cancelReason, "replaced");
  assert.equal(state.replacementResult.ok, true);
  assert.equal(state.externalResult.cancelReason, "external-signal");
  assert.deepEqual(state.calls, [
    "RequestHeaders.Replace.Pending",
    "RequestHeaders.Replace.Winner",
    "RequestHeaders.External.Pending"
  ]);
  assert.equal(actions.length, 1);
  assert.equal(actions[0].headers["x-heimdall-content-action"], "RequestHeaders.Replace.Winner");
  assert.equal(actions[0].headers.authorization, "Bearer replacement");
}

async function testInternalRequestHeaderFailuresDoNotSend(page) {
  await installFakeServer(page, {
    csrfTokens: ["should-not-be-fetched"],
    bifrostTokens: ["should-not-be-fetched"]
  });

  const state = await page.evaluate(async () => {
    window.Heimdall.config.requestHeaders = context => {
      if (context.kind === "csrf-token")
        throw new Error("csrf credentials unavailable");
      return {};
    };

    const actionResult = await window.Heimdall.invoke("RequestHeaders.CsrfFailure", {}, { swap: "none" });

    window.Heimdall.config.antiforgery = false;
    window.Heimdall.config.requestHeaders = context => {
      if (context.kind === "bifrost-token")
        throw new Error("bifrost credentials unavailable");
      return {};
    };
    document.body.innerHTML = '<div id="request-header-internal-failure"></div>';
    const sseFailure = new Promise(resolve => {
      document.addEventListener("heimdall:sse-error", event => {
        window.Heimdall.sse.disconnectAll();
        resolve({
          code: event.detail.error?.code || null,
          message: event.detail.error?.message || null
        });
      }, { once: true });
    });
    window.Heimdall.sse.connect("request-header-internal-failure", {
      element: document.querySelector("#request-header-internal-failure")
    });

    return { actionResult, sseFailure: await sseFailure };
  });

  const fetches = await getFetches(page);
  assert.equal(fetches.length, 0);
  assert.equal(state.actionResult.code, "request-headers-failed");
  assert.match(state.actionResult.error, /csrf credentials unavailable/);
  assert.equal(state.sseFailure.code, "request-headers-failed");
  assert.match(state.sseFailure.message, /bifrost credentials unavailable/);
}

async function testForbiddenDoesNotEmitUnauthorized(page) {
  await installFakeServer(page, {
    actionResponses: [{ status: 403, body: "forbidden" }]
  });

  const state = await page.evaluate(async () => {
    window.Heimdall.config.antiforgery = false;
    let unauthorized = 0;
    document.addEventListener("heimdall:unauthorized", () => unauthorized++);
    const result = await window.Heimdall.invoke("Unauthorized.Forbidden", {}, { swap: "none" });
    return { unauthorized, ok: result.ok, status: result.status };
  });

  assert.equal(state.ok, false);
  assert.equal(state.status, 403);
  assert.equal(state.unauthorized, 0);
}

async function testRejectedRequestHeadersFailClosed(page) {
  await installFakeServer(page, { actionResponses: [{ body: "should not be sent" }] });

  const state = await page.evaluate(async () => {
    window.Heimdall.config.antiforgery = false;
    window.Heimdall.config.requestHeaders = async () => {
      await Promise.resolve();
      throw new Error("access token unavailable");
    };

    const events = [];
    document.addEventListener("heimdall:error", event => {
      events.push({
        name: "error",
        code: event.detail.code,
        phase: event.detail.phase,
        message: event.detail.error?.message || null
      });
    });
    document.addEventListener("heimdall:request-after", () => events.push({ name: "after" }));
    document.addEventListener("heimdall:request-finally", event => {
      events.push({ name: "finally", code: event.detail.result?.code || null });
    });

    const result = await window.Heimdall.invoke("RequestHeaders.Rejected", {}, { swap: "none" });
    return { result, events };
  });

  assert.equal(actionFetches(await getFetches(page)).length, 0);
  assert.equal(state.result.ok, false);
  assert.equal(state.result.status, 0);
  assert.equal(state.result.code, "request-headers-failed");
  assert.match(state.result.error, /access token unavailable/);
  assert.deepEqual(state.events.map(event => event.name), ["error", "finally"]);
  assert.equal(state.events[0].code, "request-headers-failed");
  assert.equal(state.events[0].phase, "request-headers");
  assert.equal(state.events[1].code, "request-headers-failed");
}

async function testRequestHeadersRespectTimeoutCancellation(page) {
  await installFakeServer(page, { actionResponses: [{ body: "should not be sent" }] });

  const state = await page.evaluate(async () => {
    window.Heimdall.config.antiforgery = false;
    window.Heimdall.config.requestTimeoutMs = 25;
    window.Heimdall.config.requestHeaders = () => new Promise(() => {});

    const cancellations = [];
    document.addEventListener("heimdall:request-cancel", event => {
      cancellations.push(event.detail.result?.cancelReason || null);
    });

    const result = await window.Heimdall.invoke("RequestHeaders.Timeout", {}, { swap: "none" });
    return { result, cancellations };
  });

  assert.equal(actionFetches(await getFetches(page)).length, 0);
  assert.equal(state.result.cancelled, true);
  assert.equal(state.result.cancelReason, "timeout");
  assert.deepEqual(state.cancellations, ["timeout"]);
}

async function testContentUnauthorizedEventCanNavigateAndSuppressDefaultRedirect(page) {
  await installFakeServer(page, {
    actionResponses: [{ status: 401, body: "login required", location: "/default-signin" }]
  });

  const state = await page.evaluate(async () => {
    window.Heimdall.config.antiforgery = false;
    document.body.innerHTML = '<button id="unauthorized-source">Run</button><div id="unauthorized-target">Original</div>';
    const events = [];
    let redirects = 0;

    document.addEventListener("heimdall:unauthorized", event => {
      events.push({
        kind: event.detail.kind,
        actionId: event.detail.actionId,
        requestId: event.detail.requestId,
        attempt: event.detail.attempt,
        sourceId: event.detail.sourceElement?.id || null,
        url: event.detail.url,
        method: event.detail.method,
        status: event.detail.status,
        body: event.detail.body,
        redirectUrl: event.detail.redirectUrl,
        cancelable: event.cancelable
      });
      event.preventDefault();
      window.location.hash = "login-needed";
    });
    document.addEventListener("heimdall:redirect", () => redirects++);

    const result = await window.Heimdall.invoke("Unauthorized.Action", {}, {
      sourceEl: document.querySelector("#unauthorized-source"),
      target: "#unauthorized-target"
    });

    return {
      result: { ok: result.ok, status: result.status, redirectUrl: result.redirectUrl },
      events,
      redirects,
      hash: window.location.hash,
      target: document.querySelector("#unauthorized-target").textContent
    };
  });

  assert.equal(state.result.ok, false);
  assert.equal(state.result.status, 401);
  assert.equal(state.result.redirectUrl, null);
  assert.equal(state.events.length, 1);
  assert.equal(state.events[0].kind, "content-action");
  assert.equal(state.events[0].actionId, "Unauthorized.Action");
  assert.ok(state.events[0].requestId > 0);
  assert.equal(state.events[0].attempt, 1);
  assert.equal(state.events[0].sourceId, "unauthorized-source");
  assert.equal(state.events[0].method, "POST");
  assert.equal(state.events[0].status, 401);
  assert.equal(state.events[0].body, "login required");
  assert.equal(state.events[0].redirectUrl, "http://heimdall.test/default-signin");
  assert.equal(state.events[0].cancelable, true);
  assert.equal(state.redirects, 0);
  assert.equal(state.hash, "#login-needed");
  assert.equal(state.target, "Original");
}

async function testBifrostUnauthorizedEvent(page) {
  await installFakeServer(page, {
    bifrostTokenResponses: [{ status: 401, body: "expired bearer token" }]
  });

  const event = await page.evaluate(async () => {
    window.Heimdall.config.antiforgery = false;
    document.body.innerHTML = '<div id="unauthorized-sse"></div>';
    window.__unauthorizedEventSourceCount = 0;
    window.EventSource = class {
      constructor() {
        window.__unauthorizedEventSourceCount++;
      }
      addEventListener() {
      }
      close() {
      }
    };

    const detailPromise = new Promise(resolve => {
      document.addEventListener("heimdall:unauthorized", unauthorized => {
        const detail = unauthorized.detail;
        unauthorized.preventDefault();
        window.Heimdall.sse.disconnectAll();
        resolve({
          kind: detail.kind,
          topic: detail.topic,
          attempt: detail.attempt,
          status: detail.status,
          body: detail.body,
          method: detail.method,
          redirectUrl: detail.redirectUrl
        });
      }, { once: true });
    });

    window.Heimdall.sse.connect("unauthorized-topic", {
      element: document.querySelector("#unauthorized-sse")
    });

    return await detailPromise;
  });

  assert.deepEqual(event, {
    kind: "bifrost-token",
    topic: "unauthorized-topic",
    attempt: 1,
    status: 401,
    body: "expired bearer token",
    method: "GET",
    redirectUrl: null
  });
  assert.equal(await page.evaluate(() => window.__unauthorizedEventSourceCount), 0);
}

async function testCsrfUnauthorizedEvent(page) {
  await installFakeServer(page, {
    csrfTokenResponses: [{
      status: 401,
      body: "csrf authentication required",
      location: "/csrf-signin"
    }],
    actionResponses: [{ body: "should not be sent" }]
  });

  const state = await page.evaluate(async () => {
    const events = [];
    document.addEventListener("heimdall:unauthorized", event => {
      events.push({
        kind: event.detail.kind,
        status: event.detail.status,
        body: event.detail.body,
        redirectUrl: event.detail.redirectUrl,
        cancelable: event.cancelable
      });
      event.preventDefault();
    });

    let rejection = null;
    try {
      await window.Heimdall.invoke("Unauthorized.Csrf", {}, { swap: "none" });
    } catch (error) {
      rejection = { status: error.status, message: error.message };
    }

    return { events, rejection, path: window.location.pathname };
  });

  const fetches = await getFetches(page);
  assert.equal(csrfFetches(fetches).length, 1);
  assert.equal(actionFetches(fetches).length, 0);
  assert.deepEqual(state.events, [{
    kind: "csrf-token",
    status: 401,
    body: "csrf authentication required",
    redirectUrl: "http://heimdall.test/csrf-signin",
    cancelable: true
  }]);
  assert.equal(state.rejection.status, 401);
  assert.match(state.rejection.message, /CSRF token fetch failed/);
  assert.equal(state.path, "/");
}

export const tests = [
  ["awaits request headers for content, CSRF, and Bifrost token requests", testAsyncRequestHeadersAcrossHeimdallRequests],
  ["resolves queued request headers when execution begins", testQueuedRequestHeadersResolveAtExecution],
  ["resolves fresh request headers for antiforgery retries", testRequestHeadersRerunForAntiforgeryRetry],
  ["supports documented header shapes and lifecycle precedence", testRequestHeaderShapesAndLifecyclePrecedence],
  ["preserves multipart requests while adding asynchronous headers", testRequestHeadersPreserveMultipartRequests],
  ["cancels pending header providers on replacement and external abort", testPendingRequestHeadersRespectReplacementAndExternalAbort],
  ["fails closed before internal token requests are sent", testInternalRequestHeaderFailuresDoNotSend],
  ["fails closed when the request header provider rejects", testRejectedRequestHeadersFailClosed],
  ["cancels a pending request header provider on timeout", testRequestHeadersRespectTimeoutCancellation],
  ["lets unauthorized handlers navigate and suppress default redirects", testContentUnauthorizedEventCanNavigateAndSuppressDefaultRedirect],
  ["emits unauthorized events for Bifrost token requests", testBifrostUnauthorizedEvent],
  ["emits unauthorized events for CSRF token requests", testCsrfUnauthorizedEvent],
  ["does not treat forbidden responses as authentication challenges", testForbiddenDoesNotEmitUnauthorized]
];
