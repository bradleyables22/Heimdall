import assert from "node:assert/strict";
import {
  actionFetches,
  csrfFetches,
  getFetches,
  installFakeServer
} from "../helpers/runtime-page.mjs";
import { emulateTimezone } from "../helpers/timezone.mjs";

async function testInitialDocumentTimeAutoBoot(page) {
  await page.waitForFunction(() =>
    document.querySelector("#initial-document-time")?.textContent === "2026-08-26 14:30:05.123 -04:00"
  );

  assert.equal(
    await page.locator("#initial-document-time").textContent(),
    "2026-08-26 14:30:05.123 -04:00"
  );
}

async function testInitialLoadActionTime(page) {
  await page.waitForFunction(() =>
    document.querySelector("#initial-load-time")?.textContent === "2026-08-26 14:30:05.123 -04:00"
  );

  assert.equal(
    await page.locator("#initial-load-target").innerHTML(),
    '<span id="initial-load-time" heimdall-time="2026-08-26T18:30:05.123Z" heimdall-time-format="yyyy-MM-dd HH:mm:ss.fff zzz">2026-08-26 14:30:05.123 -04:00</span>'
  );

  const actions = actionFetches(await getFetches(page));
  assert.equal(actions.length, 1);
  assert.equal(actions[0].headers["x-heimdall-content-action"], "Time.InitialLoad");
}

async function testInitialTimeLocalization(page) {
  await emulateTimezone(page, "America/New_York");

  const state = await page.evaluate(() => {
    document.documentElement.lang = "en-US";
    document.body.innerHTML = `
      <span id="created"
            heimdall-time="2026-08-26T18:30:05.123Z"
            heimdall-time-format="dddd, MMMM d, yyyy 'at' h:mm:ss tt zzz">
        server fallback
      </span>
    `;

    const events = [];
    document.addEventListener("heimdall:time-before", event => {
      events.push({
        name: "before",
        id: event.detail.element.id,
        connected: event.detail.element.isConnected,
        timeZone: event.detail.timeZone,
        locale: event.detail.locale
      });
    });
    document.addEventListener("heimdall:time-after", event => {
      events.push({
        name: "after",
        id: event.detail.element.id,
        connected: event.detail.element.isConnected,
        text: event.detail.text
      });
    });

    window.Heimdall.boot(document);

    return {
      text: document.querySelector("#created").textContent,
      events
    };
  });

  assert.equal(state.text, "Wednesday, August 26, 2026 at 2:30:05 PM -04:00");
  assert.deepEqual(state.events, [
    {
      name: "before",
      id: "created",
      connected: true,
      timeZone: "America/New_York",
      locale: "en-US"
    },
    {
      name: "after",
      id: "created",
      connected: true,
      text: "Wednesday, August 26, 2026 at 2:30:05 PM -04:00"
    }
  ]);
}

async function testTimeLocalizationFormats(page) {
  await emulateTimezone(page, "America/New_York");

  const state = await page.evaluate(() => {
    document.documentElement.lang = "en-US";
    const value = "2026-08-26T18:30:05.123Z";
    document.body.innerHTML = `
      ${["d", "D", "t", "T", "g", "G"].map(format =>
        `<span id="standard-${format}" heimdall-time="${value}" heimdall-time-format="${format}">fallback</span>`
      ).join("")}
      <span id="custom-format"
            heimdall-time="${value}"
            heimdall-time-format="%d '|' yyyy-MM-dd'T'HH:mm:ss.fff">fallback</span>
      <span id="offset-source"
            heimdall-time="2026-08-26T20:30:05.123+02:00"
            heimdall-time-format="HH:mm:ss.fff">fallback</span>
      <span id="unsupported-format"
            heimdall-time="${value}"
            heimdall-time-format="O">unsupported fallback</span>
      <span id="ambiguous-single-token"
            heimdall-time="${value}"
            heimdall-time-format="M">single token fallback</span>
    `;
    const errors = [];
    document.addEventListener("heimdall:time-error", event => {
      errors.push({ id: event.detail.element.id, name: event.detail.error.name });
    });

    window.Heimdall.boot(document);

    const date = new Date(value);
    const options = {
      d: { dateStyle: "short" },
      D: { dateStyle: "full" },
      t: { timeStyle: "short" },
      T: { timeStyle: "medium" },
      g: { dateStyle: "short", timeStyle: "short" },
      G: { dateStyle: "short", timeStyle: "medium" }
    };
    const standards = {};
    const expectedStandards = {};
    for (const format of Object.keys(options)) {
      standards[format] = document.querySelector(`#standard-${format}`).textContent;
      expectedStandards[format] = new Intl.DateTimeFormat("en-US", {
        calendar: "gregory",
        timeZone: "America/New_York",
        ...options[format]
      }).format(date);
    }

    return {
      standards,
      expectedStandards,
      custom: document.querySelector("#custom-format").textContent,
      offsetSource: document.querySelector("#offset-source").textContent,
      unsupported: document.querySelector("#unsupported-format").textContent,
      ambiguousSingleToken: document.querySelector("#ambiguous-single-token").textContent,
      errors
    };
  });

  assert.deepEqual(state.standards, state.expectedStandards);
  assert.equal(state.custom, "26 | 2026-08-26T14:30:05.123");
  assert.equal(state.offsetSource, "14:30:05.123");
  assert.equal(state.unsupported, "unsupported fallback");
  assert.equal(state.ambiguousSingleToken, "single token fallback");
  assert.deepEqual(state.errors, [
    { id: "unsupported-format", name: "RangeError" },
    { id: "ambiguous-single-token", name: "RangeError" }
  ]);
}

async function testEveryCustomTimeFormat(page) {
  await emulateTimezone(page, "America/New_York");

  const state = await page.evaluate(() => {
    document.documentElement.lang = "en-US";
    const value = "2026-08-06T08:05:07.123Z";
    const cases = [
      ["day-1", "%d", "6"],
      ["day-2", "dd", "06"],
      ["day-3", "ddd", "Thu"],
      ["day-4", "dddd", "Thursday"],
      ["month-1", "%M", "8"],
      ["month-2", "MM", "08"],
      ["month-3", "MMM", "Aug"],
      ["month-4", "MMMM", "August"],
      ["year-1", "%y", "26"],
      ["year-2", "yy", "26"],
      ["year-3", "yyy", "2026"],
      ["year-4", "yyyy", "2026"],
      ["hour12-1", "%h", "4"],
      ["hour12-2", "hh", "04"],
      ["hour24-1", "%H", "4"],
      ["hour24-2", "HH", "04"],
      ["minute-1", "%m", "5"],
      ["minute-2", "mm", "05"],
      ["second-1", "%s", "7"],
      ["second-2", "ss", "07"],
      ["period-1", "%t", "A"],
      ["period-2", "tt", "AM"],
      ["fraction-1", "%f", "1"],
      ["fraction-2", "ff", "12"],
      ["fraction-3", "fff", "123"],
      ["offset-1", "%z", "-4"],
      ["offset-2", "zz", "-04"],
      ["offset-3", "zzz", "-04:00"],
      ["single-quote", "'literal' yyyy", "literal 2026"],
      ["double-quote", '\"double literal\" yyyy', "double literal 2026"],
      ["escaped", "yyyy \\y", "2026 y"],
      ["composite", "dddd, MMMM d, yyyy 'at' h:mm:ss.fff tt zzz", "Thursday, August 6, 2026 at 4:05:07.123 AM -04:00"]
    ];

    document.body.innerHTML = cases.map(([id, format]) =>
      `<span id="${id}" heimdall-time="${value}" heimdall-time-format="${format.replaceAll('"', '&quot;')}">fallback</span>`
    ).join("");
    window.Heimdall.boot(document);

    return cases.map(([id, format, expected]) => ({
      id,
      format,
      expected,
      actual: document.querySelector(`#${id}`).textContent
    }));
  });

  for (const result of state)
    assert.equal(result.actual, result.expected, `${result.id} should format ${result.format}`);
}

async function testTimeLocalizationHourBoundaries(page) {
  await emulateTimezone(page, "America/New_York");

  const state = await page.evaluate(() => {
    document.documentElement.lang = "en-US";
    document.body.innerHTML = `
      <span id="midnight"
            heimdall-time="2026-08-06T04:00:00.000Z"
            heimdall-time-format="h hh H HH t tt">fallback</span>
      <span id="noon"
            heimdall-time="2026-08-06T16:00:00.000Z"
            heimdall-time-format="h hh H HH t tt">fallback</span>
    `;

    window.Heimdall.boot(document);
    return {
      midnight: document.querySelector("#midnight").textContent,
      noon: document.querySelector("#noon").textContent
    };
  });

  assert.deepEqual(state, {
    midnight: "12 12 0 00 A AM",
    noon: "12 12 12 12 P PM"
  });
}

async function testEveryInvalidTimeFormat(page) {
  await emulateTimezone(page, "America/New_York");

  const state = await page.evaluate(() => {
    document.documentElement.lang = "en-US";
    const invalidFormats = [
      "M", "m", "y", "s", "f", "z", "h", "H",
      "O", "o", "R", "r", "U", "u", "Y",
      "ddddd", "MMMMM", "yyyyy", "hhh", "HHH", "mmm", "sss", "ttt", "ffff", "zzzz",
      "yyyy K", "yyyy F", "gg", "%D", "%", "yyyy-MM-dd\\", "yyyy 'unfinished", 'yyyy "unfinished',
      "-".repeat(257)
    ];
    const errors = [];
    document.addEventListener("heimdall:time-error", event => {
      if (event.detail.element.id.startsWith("invalid-format-"))
        errors.push(event.detail.element.id);
    });

    document.body.innerHTML = invalidFormats.map((format, index) =>
      `<span id="invalid-format-${index}"
             heimdall-time="2026-08-26T18:30:05.123Z"
             heimdall-time-format="${format.replaceAll('&', '&amp;').replaceAll('"', '&quot;')}">fallback-${index}</span>`
    ).join("");

    window.Heimdall.boot(document);
    window.Heimdall.boot(document);

    return {
      count: invalidFormats.length,
      errors,
      fallbacks: invalidFormats.map((_, index) =>
        document.querySelector(`#invalid-format-${index}`).textContent)
    };
  });

  assert.equal(state.errors.length, state.count);
  assert.equal(new Set(state.errors).size, state.count);
  assert.deepEqual(
    state.fallbacks,
    Array.from({ length: state.count }, (_, index) => `fallback-${index}`)
  );
}

async function testTimeLocalizationDaylightSaving(page) {
  await emulateTimezone(page, "America/New_York");

  const state = await page.evaluate(() => {
    document.documentElement.lang = "en-US";
    document.body.innerHTML = `
      <span id="before-transition"
            heimdall-time="2026-03-08T06:59:00.000Z"
            heimdall-time-format="yyyy-MM-dd HH:mm zzz">fallback</span>
      <span id="after-transition"
            heimdall-time="2026-03-08T07:01:00.000Z"
            heimdall-time-format="yyyy-MM-dd HH:mm zzz">fallback</span>
      <span id="winter-offset"
            heimdall-time="2026-01-15T18:30:00.000Z"
            heimdall-time-format="HH:mm zzz">fallback</span>
    `;

    window.Heimdall.boot(document);

    return {
      before: document.querySelector("#before-transition").textContent,
      after: document.querySelector("#after-transition").textContent,
      winter: document.querySelector("#winter-offset").textContent
    };
  });

  assert.deepEqual(state, {
    before: "2026-03-08 01:59 -05:00",
    after: "2026-03-08 03:01 -04:00",
    winter: "13:30 -05:00"
  });
}

async function testHalfHourTimeZone(page) {
  await emulateTimezone(page, "Asia/Kolkata");

  const text = await page.evaluate(() => {
    document.documentElement.lang = "en-US";
    document.body.innerHTML = `<span id="half-hour"
      heimdall-time="2026-08-26T18:30:00.000Z"
      heimdall-time-format="yyyy-MM-dd HH:mm zzz">fallback</span>`;
    window.Heimdall.boot(document);
    return document.querySelector("#half-hour").textContent;
  });

  assert.equal(text, "2026-08-27 00:00 +05:30");
}

async function testQuarterHourTimeZone(page) {
  await emulateTimezone(page, "Asia/Kathmandu");

  const text = await page.evaluate(() => {
    document.documentElement.lang = "en-US";
    document.body.innerHTML = `<span id="quarter-hour"
      heimdall-time="2026-08-26T18:30:00.000Z"
      heimdall-time-format="yyyy-MM-dd HH:mm zzz">fallback</span>`;
    window.Heimdall.boot(document);
    return document.querySelector("#quarter-hour").textContent;
  });

  assert.equal(text, "2026-08-27 00:15 +05:45");
}

async function testLocalizedTimeNames(page) {
  await emulateTimezone(page, "Europe/Paris");

  const text = await page.evaluate(() => {
    document.documentElement.lang = "fr-FR";
    document.body.innerHTML = `<span id="localized-names"
      heimdall-time="2026-08-26T18:30:00.000Z"
      heimdall-time-format="dddd d MMMM yyyy HH:mm zzz">fallback</span>`;
    window.Heimdall.boot(document);
    return document.querySelector("#localized-names").textContent;
  });

  assert.equal(text, "mercredi 26 août 2026 20:30 +02:00");
}

export const tests = [
  [
    "auto boots local time from the initial HTML document",
    testInitialDocumentTimeAutoBoot,
    {
      runtimePage: {
        language: "en-US",
        timezoneId: "America/New_York",
        initialBody: `<span id="initial-document-time"
                            heimdall-time="2026-08-26T18:30:05.123Z"
                            heimdall-time-format="yyyy-MM-dd HH:mm:ss.fff zzz">initial endpoint fallback</span>`
      }
    }
  ],
  [
    "localizes time returned by an initial document load action",
    testInitialLoadActionTime,
    {
      runtimePage: {
        language: "en-US",
        timezoneId: "America/New_York",
        initialBody: `<div id="initial-load-trigger"
                           heimdall-content-load="Time.InitialLoad"
                           heimdall-content-target="#initial-load-target"></div>
                      <div id="initial-load-target">load fallback</div>`,
        beforeRuntime: page => installFakeServer(page, {
          actionResponses: [{
            body: `<span id="initial-load-time"
                         heimdall-time="2026-08-26T18:30:05.123Z"
                         heimdall-time-format="yyyy-MM-dd HH:mm:ss.fff zzz">action fallback</span>`
          }]
        })
      }
    }
  ],
  ["localizes initial time elements in the browser timezone", testInitialTimeLocalization],
  ["formats documented standard and custom local-time patterns", testTimeLocalizationFormats],
  ["formats every supported custom local-time token shape", testEveryCustomTimeFormat],
  ["formats midnight and noon hour and day-period boundaries", testTimeLocalizationHourBoundaries],
  ["rejects every unsupported local-time format shape without replacing fallback", testEveryInvalidTimeFormat],
  ["formats local times across daylight-saving transitions", testTimeLocalizationDaylightSaving],
  ["formats a positive half-hour browser timezone", testHalfHourTimeZone],
  ["formats a positive quarter-hour browser timezone", testQuarterHourTimeZone],
  ["formats localized names using the document language", testLocalizedTimeNames]
];
