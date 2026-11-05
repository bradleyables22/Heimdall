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

async function testHostedCollectionForms(page, baseUrl) {
  await openHarness(page, baseUrl, "/e2e");

  for (const multipart of [false, true]) {
    for (const shape of ["properties", "parameter"]) {
      for (const values of [["Alice"], ["Alice", "", "Alice"]]) {
        await page.evaluate(({ multipart, shape, values }) => {
          document.querySelector("#e2e-collection-test")?.remove();
          const host = document.createElement("div");
          host.id = "e2e-collection-test";
          const form = document.createElement("form");
          form.id = "e2e-collection-form";
          form.setAttribute("heimdall-content-submit", `e2e.form.collections.${shape}`);
          form.setAttribute("heimdall-payload-from", "closest-form");
          form.setAttribute("heimdall-content-target", "#e2e-collection-result");

          for (const name of shape === "properties" ? ["Names", "Aliases"] : ["Names"]) {
            for (const value of values) {
              const input = document.createElement("input");
              input.type = "text";
              input.name = name;
              input.value = value;
              form.append(input);
            }
          }
          if (multipart) {
            const file = document.createElement("input");
            file.type = "file";
            file.name = "unusedFile";
            form.append(file);
          }
          const submit = document.createElement("button");
          submit.type = "submit";
          submit.textContent = "Submit collection";
          form.append(submit);
          const result = document.createElement("div");
          result.id = "e2e-collection-result";
          host.append(form, result);
          document.body.append(host);
        }, { multipart, shape, values });

        const responsePromise = page.waitForResponse(response =>
          response.request().headers()["x-heimdall-content-invocation"] === `e2e.form.collections.${shape}`);
        await page.locator("#e2e-collection-form button").click();
        const response = await responsePromise;
        assert.equal(response.status(), 200, await response.text());
        assert.ok(response.request().headers()["content-type"].startsWith(
          multipart ? "multipart/form-data" : "application/json"));

        const expected = `${values.length}:${values.join("|")}`;
        await waitForText(page.locator("#e2e-collection-result"),
          shape === "properties" ? `${expected};${expected}` : expected);
      }
    }
  }
}

export const tests = [
  ["binds state, forms, payload refs, and programmatic invokes", testPayloadsAndState],
  ["binds single and repeated form fields to collection properties and parameters", testHostedCollectionForms],
  ["uploads files through the hosted action pipeline", testHostedFileUpload]
];
