import { readFile } from "node:fs/promises";

const testOrigin = "http://heimdall.test";

export async function createRuntimePage(browser, runtime, options = {}) {
  const page = await browser.newPage();
  page.setDefaultTimeout(5000);

  const language = String(options.language || "").trim();
  const languageAttribute = language ? ` lang="${language}"` : "";
  const initialBody = options.initialBody || "";

  await page.route(`${testOrigin}/**`, route => route.fulfill({
    status: 200,
    contentType: "text/html",
    body: `<!doctype html><html${languageAttribute}><head></head><body>${initialBody}</body></html>`
  }));

  await page.goto(`${testOrigin}/`);

  if (options.timezoneId) {
    const session = await page.context().newCDPSession(page);
    await session.send("Emulation.setTimezoneOverride", { timezoneId: options.timezoneId });
    page.__heimdallTimezoneSession = session;
  }

  if (typeof options.beforeRuntime === "function")
    await options.beforeRuntime(page);

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
    const csrfTokenResponses = Array.isArray(serverOptions.csrfTokenResponses)
      ? [...serverOptions.csrfTokenResponses]
      : null;
    const bifrostTokens = Array.isArray(serverOptions.bifrostTokens) && serverOptions.bifrostTokens.length > 0
      ? [...serverOptions.bifrostTokens]
      : ["bifrost-token"];
    const bifrostTokenResponses = Array.isArray(serverOptions.bifrostTokenResponses)
      ? [...serverOptions.bifrostTokenResponses]
      : null;

    window.__heimdallFetches = [];

    function waitForResponseDelay(delayMs, signal, ignoreAbort) {
      const ms = Number(delayMs || 0);
      if (ms <= 0)
        return Promise.resolve();

      return new Promise((resolve, reject) => {
        let settled = false;
        const timer = setTimeout(() => {
          settled = true;
          resolve();
        }, ms);

        if (!ignoreAbort && signal && typeof signal.addEventListener === "function") {
          const abort = () => {
            if (settled)
              return;
            settled = true;
            clearTimeout(timer);
            reject(new DOMException("The operation was aborted.", "AbortError"));
          };

          if (signal.aborted)
            abort();
          else
            signal.addEventListener("abort", abort, { once: true });
        }
      });
    }

    window.fetch = async (input, init = {}) => {
      const url = typeof input === "string" ? input : input.url;
      const requestHeaders = Object.fromEntries(new Headers(init.headers || {}).entries());
      const isFormData = init.body instanceof FormData;
      const bodyText = init.body == null || isFormData ? null : String(init.body);
      const formBody = isFormData
        ? Array.from(init.body.entries(), ([name, value]) => ({
            name,
            value: value instanceof File
              ? { fileName: value.name, size: value.size, type: value.type }
              : value
          }))
        : null;

      let jsonBody = null;
      if (bodyText) {
        try {
          jsonBody = JSON.parse(bodyText);
        } catch {
          jsonBody = null;
        }
      }

      const fetchRecord = {
        url,
        method: init.method || "GET",
        headers: requestHeaders,
        bodyText,
        jsonBody,
        formBody,
        aborted: false
      };
      window.__heimdallFetches.push(fetchRecord);

      if (init.signal && typeof init.signal.addEventListener === "function") {
        init.signal.addEventListener("abort", () => {
          fetchRecord.aborted = true;
        }, { once: true });
      }

      if (url.includes("/__heimdall/v1/csrf")) {
        if (csrfTokenResponses) {
          const response = csrfTokenResponses.length > 1
            ? csrfTokenResponses.shift()
            : csrfTokenResponses[0];
          const responseHeaders = {
            "Content-Type": response.contentType || "application/json"
          };
          if (response.location)
            responseHeaders.Location = response.location;
          if (response.headers)
            Object.assign(responseHeaders, response.headers);

          const body = response.body != null
            ? response.body
            : JSON.stringify({ requestToken: response.token || "csrf-token" });
          const result = new Response(body, {
            status: response.status || 200,
            headers: responseHeaders
          });
          if (typeof response.redirected === "boolean")
            Object.defineProperty(result, "redirected", { value: response.redirected });
          if (response.url)
            Object.defineProperty(result, "url", { value: response.url });
          return result;
        }

        const token = csrfTokens.length > 1 ? csrfTokens.shift() : csrfTokens[0];
        return new Response(JSON.stringify({ requestToken: token }), {
          status: 200,
          headers: { "Content-Type": "application/json" }
        });
      }

      if (url.includes("/__heimdall/v1/bifrost/token")) {
        if (bifrostTokenResponses) {
          const response = bifrostTokenResponses.length > 1
            ? bifrostTokenResponses.shift()
            : bifrostTokenResponses[0];

          if (response.delayMs && Number(response.delayMs) > 0) {
            await new Promise(resolve => setTimeout(resolve, Number(response.delayMs)));
          }

          if (response.token || response.st) {
            return new Response(JSON.stringify({
              token: response.token || response.st,
              expiresInSeconds: response.expiresInSeconds || 120
            }), {
              status: response.status || 200,
              headers: { "Content-Type": "application/json" }
            });
          }

          const responseHeaders = {
            "Content-Type": response.contentType || "text/plain; charset=utf-8"
          };
          if (response.location) {
            responseHeaders.Location = response.location;
          }
          if (response.headers) {
            Object.assign(responseHeaders, response.headers);
          }

          const result = new Response(response.body || "", {
            status: response.status || 200,
            headers: responseHeaders
          });

          if (typeof response.redirected === "boolean") {
            Object.defineProperty(result, "redirected", { value: response.redirected });
          }

          if (response.url) {
            Object.defineProperty(result, "url", { value: response.url });
          }

          return result;
        }

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
        await waitForResponseDelay(response.delayMs, init.signal, response.ignoreAbort);
      }

      const responseHeaders = {
        "Content-Type": response.contentType || "text/html; charset=utf-8"
      };
      if (response.location) {
        responseHeaders.Location = response.location;
      }
      if (response.headers) {
        Object.assign(responseHeaders, response.headers);
      }

      const result = new Response(response.body || "", {
        status: response.status || 200,
        headers: responseHeaders
      });

      if (typeof response.redirected === "boolean") {
        Object.defineProperty(result, "redirected", { value: response.redirected });
      }

      if (response.url) {
        Object.defineProperty(result, "url", { value: response.url });
      }

      return result;
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
