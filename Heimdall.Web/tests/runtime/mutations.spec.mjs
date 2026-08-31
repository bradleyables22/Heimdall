import assert from "node:assert/strict";
import {
  actionFetches,
  csrfFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";

async function testMutationScopesAndIdentity(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: `
        <mutation heimdall-content-target="#panel" scope="self">
          <mutation-attr name="data-empty" value=""></mutation-attr>
          <mutation-attr name="data-remove"></mutation-attr>
          <mutation-class remove="loading stale"></mutation-class>
          <mutation-class add="ready selected"></mutation-class>
        </mutation>
        <mutation heimdall-content-target="#tree" scope="subtree">
          <mutation-attr name="data-tree" value="yes"></mutation-attr>
        </mutation>
        <mutation heimdall-content-target=".group" scope="select" selector=".shared" all>
          <mutation-attr name="data-shared" value="once"></mutation-attr>
          <mutation-class add="matched"></mutation-class>
        </mutation>
        <span id="mutation-main">Main</span>
      `
    }]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <div id="main">Old main</div>
      <section id="panel" class="loading stale keep" data-remove="yes">
        <input id="preserved" value="typed">
      </section>
      <div id="tree"><span id="branch"><b id="leaf">Leaf</b></span></div>
      <div class="group"><div class="group"><span id="shared" class="shared">Shared</span></div></div>
    `;

    const panel = document.querySelector("#panel");
    const input = document.querySelector("#preserved");
    input.__identityMarker = { preserved: true };
    let clickCount = 0;
    input.addEventListener("click", () => clickCount++);
    input.focus();

    const afterCounts = [];
    document.addEventListener("heimdall:mutation-after", event => {
      afterCounts.push({
        selector: event.detail.targetSelector,
        roots: event.detail.rootCount,
        targets: event.detail.targetCount,
        operations: event.detail.operationCount
      });
    });

    await window.Heimdall.invoke("Mutation.Scopes", {}, { target: "#main" });
    input.dispatchEvent(new MouseEvent("click", { bubbles: true }));

    return {
      samePanel: panel === document.querySelector("#panel"),
      sameInput: input === document.querySelector("#preserved"),
      marker: input.__identityMarker && input.__identityMarker.preserved,
      clickCount,
      focused: document.activeElement === input,
      value: input.value,
      panelClass: panel.className,
      panelRemovedAttribute: panel.hasAttribute("data-remove"),
      hasEmpty: panel.hasAttribute("data-empty"),
      emptyValue: panel.getAttribute("data-empty"),
      treeTargets: Array.from(document.querySelectorAll("#tree, #tree *"))
        .map(element => `${element.id}:${element.getAttribute("data-tree")}`),
      sharedClass: document.querySelector("#shared").className,
      sharedValue: document.querySelector("#shared").getAttribute("data-shared"),
      mainHtml: document.querySelector("#main").innerHTML,
      directiveCount: document.querySelectorAll("mutation, mutation-attr, mutation-class").length,
      afterCounts
    };
  });

  assert.equal(state.samePanel, true);
  assert.equal(state.sameInput, true);
  assert.equal(state.marker, true);
  assert.equal(state.clickCount, 1);
  assert.equal(state.focused, true);
  assert.equal(state.value, "typed");
  assert.equal(state.panelClass, "keep ready selected");
  assert.equal(state.panelRemovedAttribute, false);
  assert.equal(state.hasEmpty, true);
  assert.equal(state.emptyValue, "");
  assert.deepEqual(state.treeTargets, ["tree:yes", "branch:yes", "leaf:yes"]);
  assert.equal(state.sharedClass, "shared matched");
  assert.equal(state.sharedValue, "once");
  assert.equal(state.mainHtml.includes('id="mutation-main"'), true);
  assert.equal(state.directiveCount, 0);
  assert.deepEqual(state.afterCounts, [
    { selector: "#panel", roots: 1, targets: 1, operations: 4 },
    { selector: "#tree", roots: 1, targets: 3, operations: 1 },
    { selector: ".group", roots: 2, targets: 1, operations: 2 }
  ]);
}

async function testMutationCommandOrderAndMainSwap(page) {
  await installFakeServer(page, {
    actionResponses: [
      {
        body: `
          <invocation heimdall-content-target="#side">
            <template><div id="created" class="from-invocation">Side</div></template>
          </invocation>
          <mutation heimdall-content-target="#created" scope="self">
            <mutation-attr name="data-ordered" value="true"></mutation-attr>
          </mutation>
          <span id="main-applied">Main</span>
        `
      },
      {
        body: `
          <abort reason="main-only"></abort>
          <mutation heimdall-content-target="#side" scope="self">
            <mutation-class add="mutated-on-abort"></mutation-class>
          </mutation>
          <span id="main-blocked">Blocked</span>
        `
      }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="main">Old main</div><div id="side">Old side</div>';

    const first = await window.Heimdall.invoke("Mutation.Order", {}, { target: "#main" });
    const afterFirst = {
      main: document.querySelector("#main").innerHTML,
      side: document.querySelector("#side").innerHTML,
      ordered: document.querySelector("#created").getAttribute("data-ordered")
    };

    const second = await window.Heimdall.invoke("Mutation.Abort", {}, { target: "#main" });
    return {
      first: { ok: first.ok, abortSwap: first.abortSwap },
      second: { ok: second.ok, abortSwap: second.abortSwap, abortReason: second.abortReason },
      afterFirst,
      finalMain: document.querySelector("#main").innerHTML,
      sideClass: document.querySelector("#side").className
    };
  });

  assert.deepEqual(state.first, { ok: true, abortSwap: false });
  assert.equal(state.afterFirst.main.includes('id="main-applied"'), true);
  assert.equal(state.afterFirst.side, '<div id="created" class="from-invocation" data-ordered="true">Side</div>');
  assert.equal(state.afterFirst.ordered, "true");
  assert.deepEqual(state.second, { ok: true, abortSwap: true, abortReason: "main-only" });
  assert.equal(state.finalMain, state.afterFirst.main);
  assert.equal(state.sideClass, "mutated-on-abort");
}

async function testMutationLifecycleAndErrors(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: `
        <mutation heimdall-content-target="#valid" scope="self">
          <mutation-attr name="data-value" value="server"></mutation-attr>
        </mutation>
        <mutation heimdall-content-target="#cancel" scope="self">
          <mutation-class add="should-not-apply"></mutation-class>
        </mutation>
        <mutation heimdall-content-target="#valid" scope="unknown">
          <mutation-class add="invalid"></mutation-class>
        </mutation>
        <mutation heimdall-content-target="#missing" scope="self">
          <mutation-class add="missing"></mutation-class>
        </mutation>
        <span id="lifecycle-main">Main</span>
      `
    }]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = `
      <button id="source">Source</button>
      <div id="main">Old</div>
      <div id="valid"></div>
      <div id="cancel"></div>
    `;
    const events = [];

    document.addEventListener("heimdall:mutation-before", event => {
      events.push(`before:${event.detail.targetSelector}`);
      if (event.detail.targetSelector === "#valid")
        event.detail.operations[0].value = "customized";
      if (event.detail.targetSelector === "#cancel")
        event.preventDefault();
    });
    document.addEventListener("heimdall:mutation-after", event => {
      events.push(`after:${event.detail.targetSelector}:${event.detail.targetCount}:${event.detail.operationCount}`);
    });
    document.addEventListener("heimdall:mutation-error", event => {
      events.push(`error:${event.detail.code}:${event.detail.origin}`);
    });

    await window.Heimdall.invoke("Mutation.Events", {}, {
      target: "#main",
      sourceEl: document.querySelector("#source")
    });

    return {
      events,
      validValue: document.querySelector("#valid").getAttribute("data-value"),
      validClass: document.querySelector("#valid").className,
      cancelClass: document.querySelector("#cancel").className,
      mainApplied: !!document.querySelector("#lifecycle-main")
    };
  });

  assert.deepEqual(state.events, [
    "before:#valid",
    "after:#valid:1:1",
    "before:#cancel",
    "error:invalid-scope:action",
    "error:target-not-found:action"
  ]);
  assert.equal(state.validValue, "customized");
  assert.equal(state.validClass, "");
  assert.equal(state.cancelClass, "");
  assert.equal(state.mainApplied, true);
}

async function testMutationDirectiveValidation(page) {
  await installFakeServer(page, {
    actionResponses: [{
      body: `
        <mutation scope="self"><mutation-class add="x"></mutation-class></mutation>
        <mutation heimdall-content-target="#valid" scope="select">
          <mutation-class add="x"></mutation-class>
        </mutation>
        <mutation heimdall-content-target="[" scope="self">
          <mutation-class add="x"></mutation-class>
        </mutation>
        <mutation heimdall-content-target="#valid" scope="select" selector="[">
          <mutation-class add="x"></mutation-class>
        </mutation>
        <mutation heimdall-content-target="#valid" scope="self">
          <mutation-attr value="missing-name"></mutation-attr>
        </mutation>
        <mutation heimdall-content-target="#valid" scope="self">
          <mutation-attr name="bad name" value="invalid-name"></mutation-attr>
        </mutation>
        <mutation heimdall-content-target="#valid" scope="self">
          <mutation-class></mutation-class>
        </mutation>
        <mutation heimdall-content-target="#valid" scope="self">
          <mutation-class add="x" remove="y"></mutation-class>
        </mutation>
        <mutation heimdall-content-target="#valid" scope="self">
          <mutation-class add="   "></mutation-class>
        </mutation>
        <mutation heimdall-content-target="#valid" scope="self">
          <unsupported-operation></unsupported-operation>
        </mutation>
        <span id="validation-main">Main</span>
      `
    }]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="main">Old</div><div id="valid">Valid</div>';
    const errors = [];
    document.addEventListener("heimdall:mutation-error", event => {
      errors.push({ code: event.detail.code, message: event.detail.message });
    });

    await window.Heimdall.invoke("Mutation.Validation", {}, { target: "#main" });
    return {
      errors,
      validClass: document.querySelector("#valid").className,
      validAttributes: document.querySelector("#valid").getAttributeNames(),
      mainApplied: !!document.querySelector("#validation-main"),
      directives: document.querySelectorAll("mutation, mutation-attr, mutation-class").length
    };
  });

  assert.deepEqual(state.errors.map(error => error.code), [
    "missing-target",
    "missing-selector",
    "invalid-directive",
    "invalid-directive",
    "invalid-directive",
    "invalid-directive",
    "invalid-directive",
    "invalid-directive",
    "invalid-directive",
    "invalid-directive"
  ]);
  assert.equal(state.errors[0].message, "Mutation target selector is required.");
  assert.equal(state.errors[1].message, "Select-scoped mutation requires a selector.");
  assert.equal(state.errors.some(error => error.message.includes("mutation-attr requires")), true);
  assert.equal(state.errors.some(error => error.message.includes("exactly one")), true);
  assert.equal(state.errors.some(error => error.message.includes("at least one class")), true);
  assert.equal(state.errors.some(error => error.message.includes("Unsupported mutation operation")), true);
  assert.equal(state.validClass, "");
  assert.deepEqual(state.validAttributes, ["id"]);
  assert.equal(state.mainApplied, true);
  assert.equal(state.directives, 0);
}

async function testMutationBusyStateRebase(page) {
  await installFakeServer(page, {
    actionResponses: [
      {
        body: `
          <mutation heimdall-content-target="#source" scope="self">
            <mutation-attr name="disabled"></mutation-attr>
            <mutation-attr name="aria-busy" value="false"></mutation-attr>
          </mutation>
        `
      },
      {
        body: `
          <mutation heimdall-content-target="#source" scope="self">
            <mutation-attr name="disabled" value=""></mutation-attr>
            <mutation-attr name="aria-busy"></mutation-attr>
          </mutation>
        `
      }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<button id="source">Save</button>';
    const source = document.querySelector("#source");
    const during = [];
    source.addEventListener("heimdall:mutation-after", () => {
      during.push({
        disabled: source.hasAttribute("disabled"),
        ariaBusy: source.getAttribute("aria-busy")
      });
    });

    await window.Heimdall.invoke("Mutation.BusyRemove", {}, {
      sourceEl: source,
      disableElement: source
    });
    const afterRemove = {
      disabled: source.hasAttribute("disabled"),
      ariaBusy: source.getAttribute("aria-busy")
    };

    await window.Heimdall.invoke("Mutation.BusySet", {}, {
      sourceEl: source,
      disableElement: source
    });
    const afterSet = {
      disabled: source.hasAttribute("disabled"),
      ariaBusy: source.getAttribute("aria-busy")
    };

    return { during, afterRemove, afterSet };
  });

  assert.deepEqual(state.during, [
    { disabled: true, ariaBusy: "true" },
    { disabled: true, ariaBusy: "true" }
  ]);
  assert.deepEqual(state.afterRemove, { disabled: false, ariaBusy: "false" });
  assert.deepEqual(state.afterSet, { disabled: true, ariaBusy: null });
}

async function testMutationBehaviorReconciliation(page) {
  await installFakeServer(page, {
    actionResponses: [
      {
        body: `
          <mutation heimdall-content-target="#dynamic" scope="self">
            <mutation-attr name="heimdall-content-target" value="#load-result"></mutation-attr>
            <mutation-attr name="heimdall-content-load" value="Load.First"></mutation-attr>
          </mutation>
        `
      },
      { body: '<span id="first-load">First</span>' },
      {
        body: `
          <mutation heimdall-content-target="#dynamic" scope="self">
            <mutation-attr name="heimdall-content-load" value="Load.Second"></mutation-attr>
          </mutation>
        `
      },
      { body: '<span id="second-load">Second</span>' },
      {
        body: `
          <mutation heimdall-content-target="#dynamic" scope="self">
            <mutation-attr name="heimdall-content-load"></mutation-attr>
          </mutation>
        `
      }
    ]
  });

  await page.evaluate(() => {
    document.body.innerHTML = '<div id="dynamic"></div><div id="load-result">Old</div>';
    return window.Heimdall.invoke("Setup.Add");
  });
  await page.waitForSelector("#first-load");

  await page.evaluate(() => window.Heimdall.invoke("Setup.Change"));
  await page.waitForSelector("#second-load");

  await page.evaluate(async () => {
    await window.Heimdall.invoke("Setup.Remove");
    window.Heimdall.boot(document.querySelector("#dynamic"));
    await new Promise(resolve => setTimeout(resolve, 50));
  });

  const state = await page.evaluate(() => ({
    loadAttribute: document.querySelector("#dynamic").getAttribute("heimdall-content-load"),
    resultHtml: document.querySelector("#load-result").innerHTML
  }));
  const actions = actionFetches(await getFetches(page));

  assert.equal(state.loadAttribute, null);
  assert.equal(state.resultHtml, '<span id="second-load">Second</span>');
  assert.deepEqual(
    actions.map(action => action.headers["x-heimdall-content-action"]),
    ["Setup.Add", "Load.First", "Setup.Change", "Load.Second", "Setup.Remove"]
  );
}

async function testMutationSafetyBoundaries(page) {
  await installFakeServer(page, {
    actionResponses: [
      {
        status: 500,
        body: `
          <mutation heimdall-content-target="#side" scope="self">
            <mutation-class add="error-mutated"></mutation-class>
          </mutation>
          <span id="error-main">Error</span>
        `
      },
      {
        body: `
          <mutation heimdall-content-target="#side" scope="self">
            <mutation-class add="disabled-mutated"></mutation-class>
          </mutation>
          <span id="safe-main">Main</span>
        `
      },
      {
        body: `
          <mutation heimdall-content-target="#side" scope="self">
            <mutation-class add="redirect-mutated"></mutation-class>
          </mutation>
          <redirect url="#mutation-redirect"></redirect>
          <span id="redirect-main">Redirected</span>
        `
      }
    ]
  });

  const state = await page.evaluate(async () => {
    document.body.innerHTML = '<div id="main">Keep</div><div id="side">Side</div>';

    const failed = await window.Heimdall.invoke("Mutation.Error", {}, { target: "#main" });
    const afterError = {
      main: document.querySelector("#main").innerHTML,
      sideClass: document.querySelector("#side").className,
      sanitized: failed.error.includes("<mutation") === false
    };

    window.Heimdall.config.oobEnabled = false;
    const succeeded = await window.Heimdall.invoke("Mutation.Disabled", {}, { target: "#main" });
    window.Heimdall.config.oobEnabled = true;
    const redirected = await window.Heimdall.invoke("Mutation.Redirect", {}, { target: "#main" });
    return {
      failedOk: failed.ok,
      succeededOk: succeeded.ok,
      redirectUrl: redirected.redirectUrl,
      afterError,
      main: document.querySelector("#main").innerHTML,
      sideClass: document.querySelector("#side").className,
      directives: document.querySelectorAll("mutation, mutation-attr, mutation-class").length
    };
  });

  assert.equal(state.failedOk, false);
  assert.equal(state.succeededOk, true);
  assert.equal(state.redirectUrl, "#mutation-redirect");
  assert.deepEqual(state.afterError, { main: "Keep", sideClass: "", sanitized: true });
  assert.equal(state.main.includes('id="safe-main"'), true);
  assert.equal(state.sideClass, "");
  assert.equal(state.directives, 0);
}

export const tests = [
  ["mutates attributes and classes across every scope without replacing nodes", testMutationScopesAndIdentity],
  ["processes invocations and mutations in response order without suppressing main swaps", testMutationCommandOrderAndMainSwap],
  ["supports cancellable mutation lifecycle events and reports directive errors", testMutationLifecycleAndErrors],
  ["rejects malformed mutation selectors and operations without affecting main swaps", testMutationDirectiveValidation],
  ["keeps response mutations authoritative over temporary busy state", testMutationBusyStateRebase],
  ["reconciles load behavior added, changed, and removed by mutations", testMutationBehaviorReconciliation],
  ["strips mutations from errors and when out-of-band processing is disabled", testMutationSafetyBoundaries]
];
