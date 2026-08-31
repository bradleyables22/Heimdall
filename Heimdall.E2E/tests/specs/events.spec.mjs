import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import {
  assertLocalTimeMatrix,
  appProject,
  expectedLocalTimeFormats,
  openHarness,
  repoRoot,
  run,
  waitForActionAfter,
  waitForText,
  withStaticFileServer
} from "../helpers/e2e-support.mjs";

async function testDelegatedEvents(page, baseUrl) {
  await openHarness(page, baseUrl, "/e2e");

  await page.locator("#e2e-input").fill("typed");
  await waitForText(page.locator("#e2e-input-result"), "Input: typed");

  await page.locator("#e2e-change").selectOption("beta");
  await waitForText(page.locator("#e2e-change-result"), "Choice: beta");

  await page.locator("#e2e-key-input").fill("enter text");
  await page.locator("#e2e-key-input").press("Enter");
  await waitForText(page.locator("#e2e-key-result"), "Key: enter text");

  await page.locator("#e2e-blur-input").fill("blurred");
  await page.locator("#e2e-blur-input").blur();
  await waitForText(page.locator("#e2e-blur-result"), "Blur: blurred");

  await page.locator("#e2e-hover-trigger").hover();
  await waitForText(page.locator("#e2e-hover-result"), "Hover completed");
}

async function testEventBehaviorModifiers(page, baseUrl) {
  await openHarness(page, baseUrl, "/e2e");
  const result = page.locator("#e2e-behavior-result");

  await waitForText(result, "Behavior target original");
  await page.locator("#e2e-scope-self-child").click();
  await page.waitForTimeout(200);
  await waitForText(result, "Behavior target original");

  await page.locator("#e2e-scope-self-trigger").click({ position: { x: 6, y: 6 } });
  await waitForText(result, "Marker: scope self");

  await page.locator("#e2e-ignore-child").click();
  await page.waitForTimeout(200);
  await waitForText(result, "Marker: scope self");

  await page.locator("#e2e-ignore-parent").click({ position: { x: 6, y: 6 } });
  await waitForText(result, "Marker: ignore parent");

  await page.locator("#e2e-prevent-link").click();
  await waitForText(result, "Marker: prevented link");
  assert.notEqual(await page.evaluate(() => window.location.hash), "#e2e-should-not-change");

  const disableButton = page.locator("#e2e-disable-button");
  await disableButton.click();
  await page.waitForFunction(() => document.querySelector("#e2e-disable-button")?.hasAttribute("disabled") === true);
  await waitForText(page.locator("#e2e-disable-result"), "Disable completed");
  await page.waitForFunction(() => document.querySelector("#e2e-disable-button")?.hasAttribute("disabled") === false);
}

async function testNativeHtmlCommands(page, baseUrl) {
  await openHarness(page, baseUrl, "/e2e");

  const openButton = page.locator("#e2e-native-command-open");
  const closeButton = page.locator("#e2e-native-command-close");
  const dialog = page.locator("#e2e-native-command-dialog");

  assert.equal(await openButton.getAttribute("commandfor"), "e2e-native-command-dialog");
  assert.equal(await openButton.getAttribute("command"), "show-modal");
  assert.equal(await closeButton.getAttribute("commandfor"), "e2e-native-command-dialog");
  assert.equal(await closeButton.getAttribute("command"), "close");

  await openButton.click();
  await page.waitForFunction(() => document.querySelector("#e2e-native-command-dialog")?.open === true);
  assert.equal(await dialog.evaluate(element => element.matches(":modal")), true);
  await waitForText(page.locator("#e2e-native-command-message"), "Native dialog opened");

  await closeButton.click();
  await page.waitForFunction(() => document.querySelector("#e2e-native-command-dialog")?.open === false);
  assert.equal(await dialog.evaluate(element => element.matches(":modal")), false);
}

export const tests = [
  ["handles delegated input, change, keydown, blur, and hover events", testDelegatedEvents],
  ["honors scope, ignore, prevent-default, and disable modifiers", testEventBehaviorModifiers],
  ["renders and executes native HTML command helpers", testNativeHtmlCommands]
];
