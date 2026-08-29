import assert from "node:assert/strict";
import {
  actionFetches,
  csrfFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";

async function testClickTrigger(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<strong id="clicked">Clicked</strong>' }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <button id="save"
              heimdall-content-click="Buttons.Save"
              heimdall-content-target="#target">
        Save
      </button>
      <div id="target">Old</div>
    `;
  });

  await page.click("#save");
  await page.waitForSelector("#clicked");

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 1);
  assert.equal(actions[0].headers["x-heimdall-content-action"], "Buttons.Save");
}

async function testDelegatedScopeAndIgnore(page) {
  await installFakeServer(page, {
    actionResponses: [
      { body: '<span id="self-done">Self</span>' },
      { body: '<span id="inside-done">Inside</span>' }
    ]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <button id="self"
              heimdall-content-click="Scope.Self"
              heimdall-scope="self"
              heimdall-content-target="#target">
        <span id="self-child">Child</span>
      </button>
      <div id="ignored" heimdall-ignore="click">
        <button id="inside"
                heimdall-content-click="Ignore.Inside"
                heimdall-content-target="#target">
          Inside
        </button>
      </div>
      <div id="outer"
           heimdall-content-click="Ignore.Outer"
           heimdall-content-target="#target">
        <span id="outer-ignored" heimdall-ignore="click">Blocked</span>
      </div>
      <div id="target">Old</div>
    `;
  });

  await page.locator("#self-child").evaluate(el => {
    el.dispatchEvent(new MouseEvent("click", { bubbles: true, button: 0 }));
  });
  await page.locator("#self").evaluate(el => {
    el.dispatchEvent(new MouseEvent("click", { bubbles: true, button: 0 }));
  });
  await page.waitForSelector("#self-done");
  await page.click("#inside");
  await page.waitForSelector("#inside-done");
  await page.click("#outer-ignored");

  const actions = actionFetches(await getFetches(page));
  assert.deepEqual(actions.map(action => action.headers["x-heimdall-content-action"]), [
    "Scope.Self",
    "Ignore.Inside"
  ]);
}

async function testDelegatedKeydown(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="key-done">Key</span>' }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <input id="entry"
             heimdall-content-keydown="Keys.Enter"
             heimdall-key="Enter"
             heimdall-content-target="#target">
      <div id="target">Old</div>
    `;
  });

  await page.locator("#entry").press("Escape");
  await page.locator("#entry").press("Enter");
  await page.waitForSelector("#key-done");

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 1);
  assert.equal(actions[0].headers["x-heimdall-content-action"], "Keys.Enter");
}

async function testInputDebounce(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="input-done">Input</span>' }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <form id="filter-form">
        <input id="query"
               name="q"
               value=""
               heimdall-content-input="Search.Query"
               heimdall-payload-from="#filter-form"
               heimdall-content-target="#target"
               heimdall-debounce="80">
      </form>
      <div id="target">Old</div>
    `;

    const input = document.querySelector("#query");
    input.value = "a";
    input.dispatchEvent(new InputEvent("input", { bubbles: true }));
    input.value = "ab";
    input.dispatchEvent(new InputEvent("input", { bubbles: true }));
  });

  await page.waitForSelector("#input-done");

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 1);
  assert.equal(actions[0].headers["x-heimdall-content-action"], "Search.Query");
  assert.deepEqual(actions[0].jsonBody, { q: "ab" });
}

async function testHoverDelayCancel(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="hover-done">Hover</span>' }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <button id="hover"
              heimdall-content-hover="Hover.Show"
              heimdall-hover-delay="80"
              heimdall-content-target="#target">
        Hover
      </button>
      <div id="target">Old</div>
    `;

    const el = document.querySelector("#hover");
    el.dispatchEvent(new MouseEvent("mouseover", { bubbles: true, relatedTarget: document.body }));

    setTimeout(() => {
      el.dispatchEvent(new MouseEvent("mouseout", { bubbles: true, relatedTarget: document.body }));
    }, 20);

    setTimeout(() => {
      el.dispatchEvent(new MouseEvent("mouseover", { bubbles: true, relatedTarget: document.body }));
    }, 140);
  });

  await page.waitForSelector("#hover-done");

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 1);
  assert.equal(actions[0].headers["x-heimdall-content-action"], "Hover.Show");
}

async function testBootRootLoadTrigger(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="root-loaded">Root</span>' }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <div id="load-root"
           heimdall-content-load="Load.Root"
           heimdall-content-target="#target"></div>
      <div id="target">Old</div>
    `;

    window.Heimdall.boot(document.querySelector("#load-root"));
  });

  await page.waitForSelector("#root-loaded");

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 1);
  assert.equal(actions[0].headers["x-heimdall-content-action"], "Load.Root");
}

async function testMutationObserverBoot(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="loaded">Loaded</span>' }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = '<main id="host"></main>';
    document.querySelector("#host").insertAdjacentHTML(
      "beforeend",
      '<div id="load-target" heimdall-content-load="Load.Dynamic"></div>'
    );
  });

  await page.waitForSelector("#loaded");

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 1);
  assert.equal(actions[0].headers["x-heimdall-content-action"], "Load.Dynamic");
}

export const tests = [
  ["handles delegated click triggers", testClickTrigger],
  ["honors delegated trigger scope and ignore boundaries", testDelegatedScopeAndIgnore],
  ["handles delegated keydown triggers", testDelegatedKeydown],
  ["debounces delegated input triggers", testInputDebounce],
  ["cancels delayed hover triggers on mouseout", testHoverDelayCancel],
  ["boots root element load triggers", testBootRootLoadTrigger],
  ["boots inserted load triggers with MutationObserver", testMutationObserverBoot]
];
