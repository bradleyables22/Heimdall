import assert from "node:assert/strict";
import {
  actionFetches,
  csrfFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";

async function testSubmitPayload(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="saved">Saved</span>' }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <form id="form" heimdall-content-submit="Forms.Save" heimdall-content-target="#target">
        <input name="title" value="Hello">
        <input type="checkbox" name="tag" value="a" checked>
        <input type="checkbox" name="tag" value="b" checked>
        <button type="submit">Submit</button>
      </form>
      <div id="target">Old</div>
    `;
  });

  await page.locator("#form").evaluate(form => form.requestSubmit());
  await page.waitForSelector("#saved");

  const actions = actionFetches(await getFetches(page));
  assert.deepEqual(actions[0].jsonBody, { title: "Hello", tag: ["a", "b"] });
}

async function testFileUploadPayload(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="uploaded">Uploaded</span>' }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <form id="upload-form" heimdall-content-submit="Files.Upload" heimdall-content-target="#target">
        <input name="title" value="Profile photo">
        <input id="avatar" type="file" name="avatar">
        <button type="submit">Upload</button>
      </form>
      <div id="target">Old</div>
    `;
  });
  await page.locator("#avatar").setInputFiles({
    name: "avatar.txt",
    mimeType: "text/plain",
    buffer: Buffer.from("avatar bytes")
  });

  await page.locator("#upload-form").evaluate(form => form.requestSubmit());
  await page.waitForSelector("#uploaded");

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 1);
  assert.equal(actions[0].headers["content-type"], undefined);
  assert.equal(actions[0].bodyText, null);
  assert.equal(actions[0].jsonBody, null);
  assert.deepEqual(actions[0].formBody, [
    { name: "title", value: "Profile photo" },
    {
      name: "avatar",
      value: { fileName: "avatar.txt", size: 12, type: "text/plain" }
    }
  ]);
}

async function testProgrammaticFormDataPayload(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="programmatic-uploaded">Uploaded</span>' }]
  });

  await page.evaluate(async () => {
    document.body.innerHTML = '<div id="target">Old</div>';
    const data = new FormData();
    data.append("title", "Programmatic upload");
    data.append("attachment", new File(
      [new TextEncoder().encode("file bytes")],
      "notes.txt",
      { type: "text/plain" }
    ));

    await window.Heimdall.invoke("Files.Programmatic", data, {
      target: "#target"
    });
  });
  await page.waitForSelector("#programmatic-uploaded");

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 1);
  assert.equal(actions[0].headers["content-type"], undefined);
  assert.deepEqual(actions[0].formBody, [
    { name: "title", value: "Programmatic upload" },
    {
      name: "attachment",
      value: { fileName: "notes.txt", size: 10, type: "text/plain" }
    }
  ]);
}

async function testEmptyFileUploadPayload(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="optional-uploaded">Saved</span>' }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <form id="optional-upload" heimdall-content-submit="Files.Optional" heimdall-content-target="#target">
        <input name="title" value="No replacement">
        <input type="file" name="attachment">
        <button type="submit">Save</button>
      </form>
      <div id="target">Old</div>
    `;
  });

  await page.locator("#optional-upload").evaluate(form => form.requestSubmit());
  await page.waitForSelector("#optional-uploaded");

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 1);
  assert.equal(actions[0].headers["content-type"], undefined);
  assert.equal(actions[0].formBody[0].name, "title");
  assert.equal(actions[0].formBody[0].value, "No replacement");
  assert.equal(actions[0].formBody[1].name, "attachment");
  assert.equal(actions[0].formBody[1].value.fileName, "");
  assert.equal(actions[0].formBody[1].value.size, 0);
}

async function testFilePayloadSources(page) {
  await installFakeServer(page, {
    actionResponses: [
      { body: '<span id="selector-uploaded">Selector</span>' },
      { body: '<span id="closest-uploaded">Closest</span>' }
    ]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <form id="upload-source">
        <input name="title" value="Source upload">
        <input id="source-file" type="file" name="attachment">
        <button id="selector-upload"
                type="button"
                heimdall-content-click="Files.Selector"
                heimdall-payload-from="#upload-source"
                heimdall-content-target="#target">Selector</button>
        <button id="closest-upload"
                type="button"
                heimdall-content-click="Files.Closest"
                heimdall-payload-from="closest-form"
                heimdall-content-target="#target">Closest</button>
      </form>
      <div id="target">Old</div>
    `;
  });
  await page.locator("#source-file").setInputFiles({
    name: "source.txt",
    mimeType: "text/plain",
    buffer: Buffer.from("source bytes")
  });

  await page.click("#selector-upload");
  await page.waitForSelector("#selector-uploaded");
  await page.click("#closest-upload");
  await page.waitForSelector("#closest-uploaded");

  const actions = actionFetches(await getFetches(page));
  assert.deepEqual(
    actions.map(action => action.headers["x-heimdall-content-action"]),
    ["Files.Selector", "Files.Closest"]
  );
  for (const action of actions) {
    assert.equal(action.headers["content-type"], undefined);
    assert.deepEqual(action.formBody, [
      { name: "title", value: "Source upload" },
      {
        name: "attachment",
        value: { fileName: "source.txt", size: 12, type: "text/plain" }
      }
    ]);
  }
}

async function testExplicitPayloadSources(page) {
  await installFakeServer(page, {
    actionResponses: [
      { body: '<span id="static-done">Static</span>' },
      { body: '<span id="self-done">Self</span>' },
      { body: '<span id="ref-done">Ref</span>' }
    ]
  });

  await page.evaluate(() => {
    window.App = {
      Payloads: {
        Selected: {
          id: 99,
          kind: "global"
        }
      }
    };

    document.body.innerHTML = `
      <button id="static"
              heimdall-content-click="Payload.Static"
              heimdall-payload='{"id":7,"kind":"inline"}'
              heimdall-content-target="#target">Static</button>
      <button id="self"
              data-id="8"
              data-kind="dataset"
              heimdall-content-click="Payload.Self"
              heimdall-payload-from="self"
              heimdall-content-target="#target">Self</button>
      <button id="ref"
              heimdall-content-click="Payload.Ref"
              heimdall-payload-ref="App.Payloads.Selected"
              heimdall-content-target="#target">Ref</button>
      <div id="target">Old</div>
    `;
  });

  await page.click("#static");
  await page.waitForSelector("#static-done");
  await page.click("#self");
  await page.waitForSelector("#self-done");
  await page.click("#ref");
  await page.waitForSelector("#ref-done");

  const actions = actionFetches(await getFetches(page));
  assert.deepEqual(actions.map(action => action.jsonBody), [
    { id: 7, kind: "inline" },
    { id: "8", kind: "dataset" },
    { id: 99, kind: "global" }
  ]);
}

async function testClosestStatePayload(page) {
  await installFakeServer(page, {
    actionResponses: [{ body: '<span id="filtered">Filtered</span>' }]
  });

  await page.evaluate(() => {
    document.body.innerHTML = `
      <div data-heimdall-state='{"filter":"open","page":2}'>
        <button id="filter"
                heimdall-content-click="Filters.Apply"
                heimdall-payload-from="closest-state"
                heimdall-content-target="#target">
          Filter
        </button>
      </div>
      <div id="target">Old</div>
    `;
  });

  await page.click("#filter");
  await page.waitForSelector("#filtered");

  const actions = actionFetches(await getFetches(page));
  assert.deepEqual(actions[0].jsonBody, { filter: "open", page: 2 });
}

export const tests = [
  ["serializes submit payloads from forms", testSubmitPayload],
  ["submits file inputs as multipart form data", testFileUploadPayload],
  ["accepts programmatic FormData payloads", testProgrammaticFormDataPayload],
  ["keeps unselected file inputs on the multipart path", testEmptyFileUploadPayload],
  ["preserves files from explicit form payload sources", testFilePayloadSources],
  ["resolves explicit payload sources", testExplicitPayloadSources],
  ["resolves closest state payloads", testClosestStatePayload]
];
