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

async function testPayloadsAndState(page, baseUrl) {
  await openHarness(page, baseUrl, "/e2e");

  const count = page.locator("#e2e-state-count");
  await waitForText(count, "0");
  await page.locator("#e2e-state-increment").click();
  await waitForText(count, "1");
  await page.locator("#e2e-state-increment").click();
  await waitForText(count, "2");
  await page.locator("#e2e-state-decrement").click();
  await waitForText(count, "1");
  await page.locator("#e2e-state-reset").click();
  await waitForText(count, "0");

  const invokeResult = await page.evaluate(async () => {
    const result = await window.Heimdall.invoke(
      "e2e.programmatic",
      { message: "from invoke" },
      { target: "#e2e-programmatic-target" });
    return { ok: result.ok, status: result.status };
  });
  assert.deepEqual(invokeResult, { ok: true, status: 200 });
  await waitForText(page.locator("#e2e-programmatic-target"), "Programmatic: from invoke");

  await page.evaluate(() => window.HeimdallE2E.setPayload("from ref"));
  await page.locator("#e2e-payload-ref-button").click();
  await waitForText(page.locator("#e2e-payload-ref-target"), "Payload ref: from ref");

  await page.locator("#e2e-self-payload-button").click();
  await waitForText(page.locator("#e2e-self-payload-target"), "Self payload: from self");

  await page.locator("#e2e-form-submit").click();
  await waitForText(page.locator("#e2e-form-result"), "Name is required.");
  await page.locator("#e2e-name").fill("Ada");
  await page.locator("#e2e-form-submit").click();
  await waitForText(page.locator("#e2e-form-result"), "Hello, Ada.");
}

async function testHostedFileUpload(page, baseUrl) {
  await openHarness(page, baseUrl, "/e2e");

  assert.equal(
    await page.locator("#e2e-upload-form").getAttribute("enctype"),
    "multipart/form-data"
  );
  assert.equal(await page.locator("#e2e-upload-file").getAttribute("accept"), "text/plain");

  await page.locator("#e2e-upload-caption").fill("Release notes");
  await page.locator("#e2e-upload-file").setInputFiles({
    name: "notes.txt",
    mimeType: "text/plain",
    buffer: Buffer.from("upload bytes")
  });
  await page.locator("#e2e-upload-submit").click();

  await waitForText(
    page.locator("#e2e-upload-result"),
    "Uploaded: Release notes|notes.txt|12|upload bytes"
  );
}

export const tests = [
  ["binds state, forms, payload refs, and programmatic invokes", testPayloadsAndState],
  ["uploads files through the hosted action pipeline", testHostedFileUpload]
];
