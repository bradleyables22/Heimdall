import assert from "node:assert/strict";

const transport = "/__heimdall/v1/content/invocations";
const pluginId = "e2e.test-plugin";

async function openWorkbench(page, baseUrl, runtime) {
  await page.goto(`${baseUrl}/e2e/plugins?runtime=${runtime}`);
  await page.locator("#plugin-workbench").waitFor();
  await page.waitForFunction(() => document.querySelector("#plugin-editor")?.getAttribute("data-plugin-ready") === "true");
  await page.locator("#plugin-loaded-content").waitFor();
}

function observeInvocations(page) {
  const requests = [];
  page.on("request", request => {
    if (new URL(request.url()).pathname !== transport) return;
    const invocation = request.headers()["x-heimdall-content-invocation"];
    if (invocation?.startsWith("e2e.plugin.")) requests.push({ invocation, headers: request.headers(), payload: request.postDataJSON() });
  });
  return requests;
}

async function waitForValue(page, value) {
  await page.waitForFunction(value => document.querySelector("#plugin-editor")?.value === value, value);
}

async function testPackageBoundary(page, baseUrl, runtime) {
  await openWorkbench(page, baseUrl, runtime);
  for (const [path, contentType] of [
    ["/_content/Heimdall.E2E.TestPlugin/js/test-plugin.js", /javascript/],
    ["/_content/Heimdall.E2E.TestPlugin/css/test-plugin.css", /text\/css/]
  ]) {
    const response = await page.request.get(`${baseUrl}${path}`);
    assert.equal(response.status(), 200);
    assert.match(response.headers()["content-type"], contentType);
  }
  assert.deepEqual(await page.evaluate(() => Heimdall.plugins.list().map(p => [p.id, p.version])), [[pluginId, "1.0.0"]]);
  assert.equal(await page.locator("#plugin-workbench").evaluate(el => getComputedStyle(el).getPropertyValue("--test-plugin-stylesheet").trim()), "loaded");
  assert.equal(await page.locator("#plugin-loaded-content").getAttribute("data-service-label"), "Configured by E2E host");
  assert.equal(await page.locator("#plugin-loaded-content").getAttribute("data-client-label"), "host");
  assert.equal(await page.locator("#plugin-loaded-content").getAttribute("data-client-instance"), "host-1");
  assert.equal(await page.locator("#plugin-errors").getAttribute("data-count"), "0");
}

async function testTriggerPayloadAndScopes(page, baseUrl, runtime) {
  const requests = observeInvocations(page);
  await openWorkbench(page, baseUrl, runtime);
  const editor = page.locator("#plugin-editor");
  await editor.fill("  custom commit  ");
  await editor.press("Control+Enter");
  await waitForValue(page, "custom commit");
  const firstScope = await page.locator("#plugin-saved-content").getAttribute("data-request-scope");
  assert.match(firstScope, /^[a-f0-9]{32}$/);

  await editor.fill("built-in click");
  await page.locator("#plugin-save").click();
  await page.waitForFunction(() => document.querySelector("#plugin-saved-content")?.textContent === "built-in click");
  const secondScope = await page.locator("#plugin-saved-content").getAttribute("data-request-scope");
  assert.notEqual(secondScope, firstScope, "Library invocation instances must honor scoped services.");

  await editor.fill("subscribed callback");
  await page.locator("#plugin-subscription").click();
  await page.waitForFunction(() => document.querySelector("#plugin-saved-content")?.textContent === "subscribed callback");
  const saves = requests.filter(r => r.invocation === "e2e.plugin.save");
  assert.deepEqual(saves.map(r => r.payload.text), ["  custom commit  ", "built-in click", "subscribed callback"]);
  assert.ok(saves.every(r => r.headers.requestverificationtoken && r.payload.clientInstance === "host-1"));
  assert.equal(await page.locator("#plugin-saved-content").getAttribute("data-service-label"), "Configured by E2E host");
}

async function testReplacementReadiness(page, baseUrl, runtime) {
  await openWorkbench(page, baseUrl, runtime);
  const firstInstance = await page.locator("#plugin-editor").getAttribute("data-plugin-instance");
  await page.locator("#plugin-editor").fill("old draft");
  await page.locator("#plugin-replace").click();
  await waitForValue(page, "Replacement command applied");
  assert.notEqual(await page.locator("#plugin-editor").getAttribute("data-plugin-instance"), firstInstance);
  assert.equal(await page.locator("#plugin-lifecycle").getAttribute("data-disposed"), "1");
  const records = await page.locator('#plugin-command-log [data-phase="record"]').evaluateAll(entries => entries.map(e => ({ message: e.dataset.message, text: e.dataset.text, timing: e.dataset.timing })));
  assert.deepEqual(records, [
    { message: "before-replace", text: "old draft", timing: "before" },
    { message: "after-replace", text: "Replacement command applied", timing: "after" }
  ]);
  assert.equal(await page.locator('#plugin-command-log [data-phase="command"]').getAttribute("data-ready"), "true");
  assert.equal(await page.locator("#plugin-errors").getAttribute("data-count"), "0");
}

async function testAppendAndDispose(page, baseUrl, runtime) {
  await openWorkbench(page, baseUrl, runtime);
  const instance = await page.locator("#plugin-editor").getAttribute("data-plugin-instance");
  for (const expected of [2, 4]) {
    await page.locator("#plugin-append").click();
    await page.waitForFunction(expected => document.querySelectorAll('#plugin-extra-slot [data-plugin-ready="true"]').length === expected, expected);
  }
  await page.evaluate(() => { Heimdall.boot(document); Heimdall.boot(document); });
  assert.equal(await page.locator("#plugin-lifecycle").getAttribute("data-mounted"), "5");
  await page.locator("#plugin-clear").click();
  await page.waitForFunction(() => document.querySelectorAll("#plugin-extra-slot textarea").length === 0);
  assert.equal(await page.locator("#plugin-lifecycle").getAttribute("data-disposed"), "4");
  assert.equal(await page.locator("#plugin-editor").getAttribute("data-plugin-instance"), instance);
}

async function testMutationOptions(page, baseUrl, runtime) {
  await openWorkbench(page, baseUrl, runtime);
  const instance = await page.locator("#plugin-editor").getAttribute("data-plugin-instance");
  await page.locator("#plugin-mode").click();
  await page.waitForFunction(() => document.querySelector("#plugin-lifecycle")?.getAttribute("data-updated") === "1");
  await page.locator("#plugin-save").click();
  await page.waitForFunction(() => document.querySelector("#plugin-saved-content")?.getAttribute("data-mode") === "alternate");
  assert.equal(await page.locator("#plugin-editor").getAttribute("data-plugin-instance"), instance);
}

async function testAbortAndErrorIsolation(page, baseUrl, runtime) {
  await openWorkbench(page, baseUrl, runtime);
  const instance = await page.locator("#plugin-editor").getAttribute("data-plugin-instance");
  await page.locator("#plugin-abort").click();
  await waitForValue(page, "Abort command applied");
  assert.equal(await page.locator("#plugin-should-not-render").count(), 0);
  assert.equal(await page.locator("#plugin-editor").getAttribute("data-plugin-instance"), instance);
  await page.locator("#plugin-failure").click();
  await page.locator('#plugin-command-log [data-message="recovered-after-failure"]').waitFor();
  assert.equal(await page.locator("#plugin-errors").getAttribute("data-count"), "1");
  assert.match(await page.locator("#plugin-errors").textContent(), /command\/command-failed/);
  assert.equal(await page.locator("heimdall-command").count(), 0);
}

async function testBifrostOrdering(page, baseUrl, runtime) {
  await openWorkbench(page, baseUrl, runtime);
  await page.waitForFunction(() => document.querySelector("#plugin-stream-host")?.getAttribute("data-connection") === "open");
  await page.locator("#plugin-publish").click();
  await waitForValue(page, "SSE second");
  const commands = await page.locator('#plugin-command-log [data-phase="command"][data-origin="sse"]').evaluateAll(entries => entries.map(e => e.dataset.text));
  assert.deepEqual(commands, ["SSE first", "SSE second"]);
  assert.equal(await page.locator("#plugin-errors").getAttribute("data-count"), "0");
}

async function testAntiforgery(page, baseUrl, runtime) {
  await openWorkbench(page, baseUrl, runtime);
  const response = await page.request.post(`${baseUrl}${transport}`, {
    headers: { "X-Heimdall-Content-Invocation": "e2e.plugin.save" },
    data: { text: "Unprotected request" }
  });
  assert.equal(response.status(), 400);
  assert.equal(await page.locator("#plugin-saved-content").count(), 0);
  await page.locator("#plugin-editor").fill("Protected request");
  await page.locator("#plugin-editor").press("Control+Enter");
  await waitForValue(page, "Protected request");
  await page.locator("#plugin-saved-content").waitFor();
}

async function testReplacementCancellation(page, baseUrl, runtime) {
  await openWorkbench(page, baseUrl, runtime);
  const editor = page.locator("#plugin-editor");
  await editor.evaluate(el => { el.dataset.requestDelay = "200"; });
  await editor.fill("slow request");
  const first = page.waitForRequest(r => new URL(r.url()).pathname === transport && r.postDataJSON()?.text === "slow request");
  await editor.press("Control+Enter");
  await first;
  await editor.evaluate(el => { el.dataset.requestDelay = "0"; });
  await editor.fill("fast request");
  await editor.press("Control+Enter");
  await page.waitForFunction(() => document.querySelector("#plugin-saved-content")?.textContent === "fast request");
  await waitForValue(page, "fast request");
  const commands = await page.locator('#plugin-command-log [data-phase="command"]').evaluateAll(entries => entries.map(e => e.dataset.text));
  assert.deepEqual(commands, ["fast request"]);
}

async function testRemovalAndReinstallation(page, baseUrl, runtime) {
  const requests = observeInvocations(page);
  await openWorkbench(page, baseUrl, runtime);
  const before = requests.length;
  assert.equal(await page.evaluate(() => Heimdall.plugins.remove("e2e.test-plugin")), true);
  await page.locator("#plugin-editor").press("Control+Enter");
  await page.locator("#plugin-subscription").click();
  assert.equal(requests.length, before);
  assert.equal(await page.locator("#plugin-lifecycle").getAttribute("data-disposed"), "1");
  assert.equal(await page.locator("#plugin-lifecycle").getAttribute("data-unsubscribed"), "1");
  await page.evaluate(async () => {
    const module = await import("/js/e2e-test-plugin.js");
    Heimdall.use(module.testPlugin, module.pluginOptions);
  });
  await page.waitForFunction(() => document.querySelector("#plugin-editor")?.getAttribute("data-plugin-ready") === "true");
  await page.locator("#plugin-editor").fill("Reinstalled plugin");
  await page.locator("#plugin-editor").press("Control+Enter");
  await page.waitForFunction(() => document.querySelector("#plugin-saved-content")?.textContent === "Reinstalled plugin");
  assert.equal(requests.filter(r => r.invocation === "e2e.plugin.save").length, 1);
}

const scenarios = [
  ["loads plugin RCL assets and initializes before library invocations", testPackageBoundary],
  ["sends plugin and built-in triggers through real payload binding and scoped DI", testTriggerPayloadAndScopes],
  ["initializes replaced plugin elements before response commands", testReplacementReadiness],
  ["mounts all appended plugin roots and disposes removed subtrees", testAppendAndDispose],
  ["updates plugin behavior options through server mutations", testMutationOptions],
  ["runs plugin commands on abort and isolates handler errors", testAbortAndErrorIsolation],
  ["delivers ordered asynchronous plugin commands over real Bifrost", testBifrostOrdering],
  ["enforces real antiforgery for library plugin invocations", testAntiforgery],
  ["cancels stale plugin invocations before response effects", testReplacementCancellation],
  ["removes and reinstalls the hosted plugin without duplicate subscriptions", testRemovalAndReinstallation]
];

export const tests = ["minified", "debug"].flatMap(runtime => scenarios.map(([name, test]) => [
  `${name} (${runtime})`, (page, baseUrl) => test(page, baseUrl, runtime)
]));
