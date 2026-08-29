import assert from "node:assert/strict";
import {
  actionFetches,
  csrfFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";

async function testSwapLifecycleContract(page) {
  await installFakeServer(page, {
    actionResponses: [
      {
        body: '<invocation heimdall-content-target="#side"><template><b id="side-new">Side</b></template></invocation><span id="main-new">Main</span>'
      },
      { body: '<span id="blocked">Blocked</span>' }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target">Old</div><div id="side">Old side</div>';
    const events = [];

    document.addEventListener("heimdall:swap-before", event => {
      const actionId = event.detail.requestContext && event.detail.requestContext.actionId;
      events.push(`before:${event.detail.origin}:${event.detail.kind}:${actionId}`);

      if (actionId === "Swap.Events" && event.detail.kind === "main") {
        const script = document.createElement("script");
        script.textContent = "window.__swapScriptRan = true";
        event.detail.fragment.append(script);
      }

      if (actionId === "Swap.Cancel")
        event.preventDefault();
    });
    document.addEventListener("heimdall:swap-after", event => {
      const actionId = event.detail.requestContext && event.detail.requestContext.actionId;
      events.push(`after:${event.detail.origin}:${event.detail.kind}:${actionId}`);
    });

    const applied = await window.Heimdall.invoke("Swap.Events", {}, { target: "#target" });
    const cancelled = await window.Heimdall.invoke("Swap.Cancel", {}, { target: "#target" });

    return {
      appliedOk: applied.ok,
      cancelledOk: cancelled.ok,
      events,
      targetHtml: document.querySelector("#target").innerHTML,
      sideHtml: document.querySelector("#side").innerHTML,
      scriptRan: window.__swapScriptRan === true,
      scripts: document.querySelectorAll("#target script, #side script").length
    };
  });

  assert.equal(state.appliedOk, true);
  assert.equal(state.cancelledOk, true);
  assert.deepEqual(state.events, [
    "before:action:invocation:Swap.Events",
    "after:action:invocation:Swap.Events",
    "before:action:main:Swap.Events",
    "after:action:main:Swap.Events",
    "before:action:main:Swap.Cancel"
  ]);
  assert.equal(state.targetHtml, '<span id="main-new">Main</span>');
  assert.equal(state.sideHtml, '<b id="side-new">Side</b>');
  assert.equal(state.scriptRan, false);
  assert.equal(state.scripts, 0);
}

async function testActionErrorSanitization(page) {
  await installFakeServer(page, {
    actionResponses: [{
      status: 500,
      body: '<script>window.__bad = true;</script><javascript function="window.App.bad"></javascript><invocation heimdall-content-target="#side"><template>Side</template></invocation><abort reason="x"></abort><redirect url="#bad"></redirect><span id="server-error">Boom</span>'
    }]
  });

  const state = await page.evaluate(async () => {
    window.App = {
      bad() {
        window.__jsBad = true;
      }
    };
    document.body.innerHTML = '<div id="target">Keep</div><div id="side">Side keep</div>';
    const seen = [];
    const result = await window.Heimdall.invoke("Errors.Save", {}, {
      target: "#target",
      onError: errorResult => seen.push({
        ok: errorResult.ok,
        status: errorResult.status,
        error: errorResult.error
      })
    });

    return {
      result,
      seen,
      targetHtml: document.querySelector("#target").innerHTML,
      sideHtml: document.querySelector("#side").innerHTML,
      badRan: window.__bad === true,
      jsBadRan: window.__jsBad === true
    };
  });

  assert.equal(state.result.ok, false);
  assert.equal(state.result.status, 500);
  assert.equal(state.result.html, null);
  assert.equal(state.targetHtml, "Keep");
  assert.equal(state.sideHtml, "Side keep");
  assert.equal(state.badRan, false);
  assert.equal(state.jsBadRan, false);
  assert.equal(state.seen.length, 1);
  assert.equal(state.seen[0].status, 500);
  assert.ok(state.result.error.includes('<span id="server-error">Boom</span>'));
  assert.equal(state.result.error.includes("<script"), false);
  assert.equal(state.result.error.includes("<javascript"), false);
  assert.equal(state.result.error.includes("<invocation"), false);
  assert.equal(state.result.error.includes("<abort"), false);
  assert.equal(state.result.error.includes("<redirect"), false);
}

async function testNetworkFailure(page) {
  await installFakeServer(page);

  const state = await page.evaluate(async () => {
    const originalFetch = window.fetch;
    window.fetch = async (input, init) => {
      const url = typeof input === "string" ? input : input.url;
      if (url.includes("/__heimdall/v1/content/actions")) {
        throw new Error("network down");
      }
      return originalFetch(input, init);
    };

    const errors = [];
    document.addEventListener("heimdall:error", ev => {
      errors.push({
        actionId: ev.detail.actionId,
        message: ev.detail.error && ev.detail.error.message
      });
    });

    document.body.innerHTML = '<div id="target">Keep</div>';
    const result = await window.Heimdall.invoke("Network.Fail", {}, { target: "#target" });

    return {
      result,
      errors,
      targetHtml: document.querySelector("#target").innerHTML
    };
  });

  assert.equal(state.result.ok, false);
  assert.equal(state.result.status, 0);
  assert.equal(state.result.error, "network down");
  assert.equal(state.targetHtml, "Keep");
  assert.equal(state.errors.length, 1);
  assert.equal(state.errors[0].actionId, "Network.Fail");
  assert.equal(state.errors[0].message, "network down");
}

async function testNonSerializablePayload(page) {
  await installFakeServer(page);

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target">Keep</div>';
    const payload = {};
    payload.self = payload;

    const errors = [];
    document.addEventListener("heimdall:error", ev => {
      errors.push({
        actionId: ev.detail.actionId,
        status: ev.detail.status,
        message: ev.detail.error && ev.detail.error.message
      });
    });

    try {
      await window.Heimdall.invoke("Payload.Circular", payload, { target: "#target" });
      return { threw: false, errors };
    } catch (error) {
      return {
        threw: true,
        message: error.message,
        causeName: error.cause && error.cause.name,
        errors
      };
    }
  });

  assert.equal(state.threw, true);
  assert.equal(state.message, "Heimdall payload is not JSON-serializable for action 'Payload.Circular'.");
  assert.equal(state.causeName, "TypeError");
  assert.equal(state.errors.length, 1);
  assert.equal(state.errors[0].actionId, "Payload.Circular");
  assert.equal(state.errors[0].status, 0);
}

async function testCustomHeadersAndDisableState(page) {
  await installFakeServer(page, {
    actionResponses: [
      { body: '<span id="header-done">Headers</span>' },
      { body: '<span id="disable-done">Disabled</span>', delayMs: 40 }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <div id="target">Old</div>
      <button id="save"
              heimdall-content-click="Disable.Save"
              heimdall-content-target="#target">
        Save
      </button>
    `;

    await window.Heimdall.invoke("Headers.Save", {}, {
      target: "#target",
      headers: {
        "X-Custom": "custom-value"
      }
    });

    const button = document.querySelector("#save");
    const beforeState = new Promise(resolve => {
      document.addEventListener("heimdall:before", () => {
        if (button.hasAttribute("disabled")) {
          resolve({
            disabled: button.hasAttribute("disabled"),
            busy: button.getAttribute("aria-busy")
          });
        }
      }, { once: true });
    });

    const afterState = new Promise(resolve => {
      document.addEventListener("heimdall:after", () => {
        setTimeout(() => resolve({
          disabled: button.hasAttribute("disabled"),
          busy: button.getAttribute("aria-busy"),
          targetHtml: document.querySelector("#target").innerHTML
        }), 0);
      }, { once: true });
    });

    button.click();

    return {
      before: await beforeState,
      after: await afterState
    };
  });

  assert.deepEqual(state.before, { disabled: true, busy: "true" });
  assert.deepEqual(state.after, {
    disabled: false,
    busy: null,
    targetHtml: '<span id="disable-done">Disabled</span>'
  });

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 2);
  assert.equal(actions[0].headers["x-custom"], "custom-value");
  assert.equal(actions[0].headers["x-heimdall-content-action"], "Headers.Save");
  assert.equal(actions[1].headers["x-heimdall-content-action"], "Disable.Save");
}

export const tests = [
  ["emits cancellable swap lifecycle events", testSwapLifecycleContract],
  ["sanitizes error HTML and calls error callbacks", testActionErrorSanitization],
  ["returns network failures without throwing", testNetworkFailure],
  ["rejects non-serializable payloads with error events", testNonSerializablePayload],
  ["merges custom headers and restores disabled triggers", testCustomHeadersAndDisableState]
];
