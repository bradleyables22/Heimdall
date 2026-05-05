import { readFile } from "node:fs/promises";

const testOrigin = "http://heimdall.test";

export async function createRuntimePage(browser, runtime) {
  const page = await browser.newPage();
  page.setDefaultTimeout(5000);

  await page.route(`${testOrigin}/**`, route => route.fulfill({
    status: 200,
    contentType: "text/html",
    body: "<!doctype html><html><head></head><body></body></html>"
  }));

  await page.goto(`${testOrigin}/`);
  await page.addScriptTag({ content: await readFile(runtime.path, "utf8") });
  await page.waitForFunction(() => !!window.Heimdall);

  return page;
}

export async function installFakeServer(page, options = {}) {
  await page.evaluate((serverOptions) => {
    const actionResponses = Array.isArray(serverOptions.actionResponses)
      ? [...serverOptions.actionResponses]
      : [];
    const csrfTokens = Array.isArray(serverOptions.csrfTokens) && serverOptions.csrfTokens.length > 0
      ? [...serverOptions.csrfTokens]
      : ["csrf-token"];
    const bifrostTokens = Array.isArray(serverOptions.bifrostTokens) && serverOptions.bifrostTokens.length > 0
      ? [...serverOptions.bifrostTokens]
      : ["bifrost-token"];

    window.__heimdallFetches = [];

    window.fetch = async (input, init = {}) => {
      const url = typeof input === "string" ? input : input.url;
      const headers = Object.fromEntries(new Headers(init.headers || {}).entries());
      const bodyText = init.body == null ? null : String(init.body);

      let jsonBody = null;
      if (bodyText) {
        try {
          jsonBody = JSON.parse(bodyText);
        } catch {
          jsonBody = null;
        }
      }

      window.__heimdallFetches.push({
        url,
        method: init.method || "GET",
        headers,
        bodyText,
        jsonBody
      });

      if (url.includes("/__heimdall/v1/csrf")) {
        const token = csrfTokens.length > 1 ? csrfTokens.shift() : csrfTokens[0];
        return new Response(JSON.stringify({ requestToken: token }), {
          status: 200,
          headers: { "Content-Type": "application/json" }
        });
      }

      if (url.includes("/__heimdall/v1/bifrost/token")) {
        const token = bifrostTokens.length > 1 ? bifrostTokens.shift() : bifrostTokens[0];
        return new Response(JSON.stringify({ token, expiresInSeconds: 120 }), {
          status: 200,
          headers: { "Content-Type": "application/json" }
        });
      }

      const response = actionResponses.length > 0
        ? actionResponses.shift()
        : { status: 200, body: "" };

      if (response.delayMs && Number(response.delayMs) > 0) {
        await new Promise(resolve => setTimeout(resolve, Number(response.delayMs)));
      }

      return new Response(response.body || "", {
        status: response.status || 200,
        headers: {
          "Content-Type": response.contentType || "text/html; charset=utf-8"
        }
      });
    };
  }, options);
}

export async function getFetches(page) {
  return page.evaluate(() => window.__heimdallFetches || []);
}

export function actionFetches(fetches) {
  return fetches.filter(fetch => fetch.url.includes("/__heimdall/v1/content/actions"));
}

export function csrfFetches(fetches) {
  return fetches.filter(fetch => fetch.url.includes("/__heimdall/v1/csrf"));
}
