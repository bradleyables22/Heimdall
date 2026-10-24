import assert from "node:assert/strict";
import { openHarness, waitForActionAfter } from "../helpers/e2e-support.mjs";

async function testStaticBlazorComponentRendersThroughAction(page, baseUrl) {
  await openHarness(page, baseUrl, "/e2e");

  const trigger = page.locator("#e2e-blazor-button");
  await page.locator("#e2e-blazor-component").waitFor();
  const form = page.locator("#e2e-blazor-form");
  assert.equal(await form.getAttribute("heimdall-content-submit"), "e2e.blazor.render");
  assert.equal(await form.getAttribute("heimdall-payload-from"), "closest-form");
  assert.equal(await form.getAttribute("heimdall-content-target"), "#e2e-blazor-target");

  const actionCompleted = waitForActionAfter(page, "e2e.blazor.render");
  await trigger.click();
  await actionCompleted;

  const target = page.locator("#e2e-blazor-target");
  const component = target.locator("[data-blazor-name]");
  await component.waitFor();

  assert.equal(await component.getAttribute("data-blazor-name"), "Ada <Lovelace>");
  assert.equal(await target.locator("[data-blazor-message]").textContent(), "Ada <Lovelace>");
  await target.locator("[data-blazor-initialized]").waitFor();
  assert.equal(await target.locator("[data-blazor-initialized]").textContent(), "initialized");
}

async function testStaticBlazorComponentRemainsNonInteractiveAndCanBeReplaced(page, baseUrl) {
  await openHarness(page, baseUrl, "/e2e");

  let actionCompleted = waitForActionAfter(page, "e2e.blazor.render");
  await page.locator("#e2e-blazor-button").click();
  await actionCompleted;

  const target = page.locator("#e2e-blazor-target");
  assert.equal(await target.locator("button").getAttribute("onclick"), null);

  actionCompleted = waitForActionAfter(page, "e2e.blazor.render");
  await page.locator("#e2e-blazor-second-button").click();
  await actionCompleted;

  assert.equal(
    await target.locator("[data-blazor-name]").getAttribute("data-blazor-name"),
    "Grace Hopper"
  );
  assert.equal(await target.locator("[data-blazor-message]").textContent(), "Grace Hopper");
}

async function testStaticBlazorComponentKeepsNativeFormsNative(page, baseUrl) {
  await openHarness(page, baseUrl, "/e2e");

  const form = page.locator("#e2e-blazor-native-form");
  assert.equal(await form.getAttribute("heimdall-content-submit"), null);

  const navigation = page.waitForURL(url => {
    return url.pathname === "/e2e" && url.searchParams.get("native") === "true";
  });

  await page.locator("#e2e-blazor-native-button").click();
  await navigation;
  await page.locator("#e2e-blazor-component").waitFor();
}

export const tests = [
  ["renders a static Blazor component through a Heimdall action", testStaticBlazorComponentRendersThroughAction],
  ["keeps static Blazor output non-interactive and replaceable", testStaticBlazorComponentRemainsNonInteractiveAndCanBeReplaced],
  ["keeps native Blazor forms native", testStaticBlazorComponentKeepsNativeFormsNative]
];
