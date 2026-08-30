(() => {
  // core/utils.js
  function isElement(x) {
    return x && x.nodeType === 1;
  }
  function resolveTarget(target, fallbackEl) {
    if (!target)
      return fallbackEl || null;
    if (isElement(target))
      return target;
    if (typeof target === "string")
      return document.querySelector(target);
    return fallbackEl || null;
  }
  function safeJsonParse(text) {
    try {
      return JSON.parse(text);
    } catch {
      return null;
    }
  }
  async function safeText(res) {
    try {
      return await res.text();
    } catch {
      return "";
    }
  }
  function onReady(fn) {
    if (document.readyState === "loading") {
      document.addEventListener("DOMContentLoaded", fn, { once: true });
    } else {
      fn();
    }
  }
  function getAttr(el, name) {
    const v = el.getAttribute(name);
    return v == null ? null : v;
  }
  function truthyAttr(el, name, defaultValue) {
    const v = getAttr(el, name);
    if (v == null)
      return !!defaultValue;
    const s = String(v).toLowerCase().trim();
    if (s === "" || s === "true" || s === "1" || s === "yes")
      return true;
    if (s === "false" || s === "0" || s === "no")
      return false;
    return !!defaultValue;
  }
  function intAttr(el, name, defaultValue) {
    const v = getAttr(el, name);
    if (v == null || v === "")
      return defaultValue;
    const n = parseInt(v, 10);
    return Number.isFinite(n) ? n : defaultValue;
  }
  function formDataToObject(fd) {
    const obj = {};
    for (const [k, v] of fd.entries()) {
      if (Object.prototype.hasOwnProperty.call(obj, k)) {
        if (!Array.isArray(obj[k])) obj[k] = [obj[k]];
        obj[k].push(v);
      } else {
        obj[k] = v;
      }
    }
    return obj;
  }
  function formDataToPayload(fd) {
    for (const value of fd.values()) {
      if (typeof Blob !== "undefined" && value instanceof Blob)
        return fd;
    }
    return formDataToObject(fd);
  }
  function getByPath(root, path) {
    if (!path)
      return void 0;
    let cur = root;
    const parts = String(path).split(".").map((p) => p.trim()).filter(Boolean);
    for (const p of parts) {
      if (cur == null)
        return void 0;
      cur = cur[p];
    }
    return cur;
  }

  // core/auth-redirects.js
  function getCurrentPageReturnUrl(global) {
    const location = global.location;
    if (!location)
      return "/";
    return `${location.pathname || "/"}${location.search || ""}${location.hash || ""}`;
  }
  function getMatchingSearchParamNames(searchParams, name) {
    const lowerName = String(name || "").toLowerCase();
    const matches = [];
    for (const key of searchParams.keys()) {
      if (String(key || "").toLowerCase() === lowerName && !matches.includes(key))
        matches.push(key);
    }
    return matches;
  }
  function normalizeFollowedAuthRedirectUrl(global, getConfig, redirectUrl) {
    const config = typeof getConfig === "function" ? getConfig() || {} : {};
    const returnUrlParameter = config.authReturnUrlParameter || "ReturnUrl";
    if (!returnUrlParameter)
      return redirectUrl;
    try {
      const nextUrl = new URL(redirectUrl, global.location?.origin || void 0);
      const matchingNames = getMatchingSearchParamNames(nextUrl.searchParams, returnUrlParameter);
      if (matchingNames.length === 0)
        return nextUrl.toString();
      for (const name of matchingNames)
        nextUrl.searchParams.set(name, getCurrentPageReturnUrl(global));
      return nextUrl.toString();
    } catch {
      return redirectUrl;
    }
  }
  function getAuthRedirectUrlFromResponse(response) {
    if (!response)
      return null;
    if (response.redirected && response.url)
      return response.url;
    const status = Number(response.status);
    if (status !== 401 && status !== 403)
      return null;
    try {
      return response.headers && typeof response.headers.get === "function" ? response.headers.get("Location") : null;
    } catch {
      return null;
    }
  }

  // core/request-headers.js
  var REQUEST_HEADERS_FAILED_CODE = "request-headers-failed";
  function createRequestHeadersRuntime({ global, getConfig }) {
    function abortError() {
      if (typeof global.DOMException === "function")
        return new global.DOMException("The request was aborted.", "AbortError");
      const error = new Error("The request was aborted.");
      error.name = "AbortError";
      return error;
    }
    function waitForProvider(value, signal) {
      const promise = Promise.resolve(value);
      if (!signal || typeof signal.addEventListener !== "function")
        return promise;
      if (signal.aborted)
        return Promise.reject(abortError());
      return new Promise((resolve2, reject) => {
        let settled = false;
        const onAbort = () => {
          if (settled)
            return;
          settled = true;
          reject(abortError());
        };
        signal.addEventListener("abort", onAbort, { once: true });
        promise.then(
          (result) => {
            if (settled)
              return;
            settled = true;
            signal.removeEventListener("abort", onAbort);
            resolve2(result);
          },
          (error) => {
            if (settled)
              return;
            settled = true;
            signal.removeEventListener("abort", onAbort);
            reject(error);
          }
        );
      });
    }
    function sourceEntries(source) {
      if (source == null)
        return [];
      if (typeof global.Headers === "function" && source instanceof global.Headers)
        return Array.from(source.entries());
      if (Array.isArray(source))
        return source;
      if (typeof source === "object")
        return Object.entries(source);
      throw new TypeError("requestHeaders must return a Headers instance, header pairs, an object, or null.");
    }
    function validatedEntry(entry) {
      if (!Array.isArray(entry) || entry.length !== 2)
        throw new TypeError("requestHeaders header pairs must contain exactly a name and value.");
      const name = String(entry[0]);
      const value = entry[1];
      if (typeof global.Headers !== "function")
        return [name, String(value)];
      const validation = new global.Headers([[name, value]]);
      return [name, validation.get(name) || ""];
    }
    function setHeader(target, name, value) {
      const lowerName = name.toLowerCase();
      for (const existing of Object.keys(target)) {
        if (existing.toLowerCase() === lowerName)
          delete target[existing];
      }
      target[name] = value;
    }
    function merge(target, source) {
      for (const entry of sourceEntries(source)) {
        const [name, value] = validatedEntry(entry);
        setHeader(target, name, value);
      }
      return target;
    }
    async function resolve(context, initialHeaders) {
      const headers = merge({}, initialHeaders);
      const configured = getConfig()?.requestHeaders;
      if (configured == null)
        return headers;
      try {
        const providerContext = {
          ...context,
          headers
        };
        const provided = typeof configured === "function" ? await waitForProvider(configured(providerContext), context?.signal) : configured;
        const normalized = merge({}, headers);
        merge(normalized, provided);
        return normalized;
      } catch (cause) {
        const kind = context?.kind || "request";
        const message = cause && cause.message ? cause.message : String(cause || "Unknown request header provider failure.");
        const error = new Error(`Heimdall requestHeaders failed for ${kind}: ${message}`);
        error.name = "HeimdallRequestHeadersError";
        error.code = REQUEST_HEADERS_FAILED_CODE;
        error.requestKind = kind;
        error.cause = cause;
        throw error;
      }
    }
    return {
      merge,
      resolve
    };
  }

  // core/unauthorized.js
  function emitUnauthorized({
    response,
    emitLifecycle,
    sourceElement,
    detail
  }) {
    if (Number(response?.status) !== 401 || typeof emitLifecycle !== "function")
      return true;
    return emitLifecycle(
      sourceElement || null,
      "heimdall:unauthorized",
      {
        ...detail,
        status: 401,
        response
      },
      { cancelable: true }
    );
  }

  // core/actions.js
  function createActionInvoker({
    global,
    getConfig,
    ensureCsrfToken,
    clearCsrfToken,
    emit,
    emitLifecycle,
    dbg,
    payloadBindingFromElement,
    boot,
    dom,
    historyRuntime,
    coordinator,
    busyState,
    resolveRequestHeaders,
    mergeRequestHeaders,
    getClientInfoHeader,
    actionHeader,
    csrfHeader,
    clientInfoHeader
  }) {
    let nextRequestId = 0;
    const payloadStates = /* @__PURE__ */ new WeakMap();
    const targetStates = /* @__PURE__ */ new WeakMap();
    function payloadSignature(value) {
      try {
        return JSON.stringify(value);
      } catch {
        return null;
      }
    }
    function installPayloadState(context, payload, binding) {
      const state = {
        value: payload,
        binding: binding || null,
        assigned: false,
        initialSignature: payloadSignature(payload),
        refreshAtExecution: false
      };
      Object.defineProperty(context, "payload", {
        configurable: true,
        enumerable: true,
        get() {
          return state.value;
        },
        set(value) {
          state.assigned = true;
          state.value = value;
        }
      });
      payloadStates.set(context, state);
    }
    function finalizePayloadConfiguration(context) {
      const state = payloadStates.get(context);
      if (!state || !state.binding)
        return;
      const changedInPlace = payloadSignature(state.value) !== state.initialSignature;
      state.refreshAtExecution = !state.assigned && !changedInPlace;
    }
    function refreshExecutionPayload(context) {
      const state = payloadStates.get(context);
      if (!state || !state.refreshAtExecution || !state.binding)
        return true;
      const resolved = state.binding.resolve();
      if (!resolved || !resolved.available)
        return false;
      state.value = resolved.value;
      return true;
    }
    function initializeTargetState(context) {
      const source = context.target;
      const resolved = resolveTarget(source, context.fallbackTarget);
      const state = {
        source,
        resolved,
        wasConnected: !!(resolved && resolved.isConnected)
      };
      context.target = resolved;
      targetStates.set(context, state);
    }
    function refreshTarget(context, rejectDisconnected) {
      const state = targetStates.get(context);
      if (!state)
        return true;
      if (context.target !== state.resolved) {
        state.source = context.target;
        state.wasConnected = !!(context.target && context.target.isConnected);
      }
      if (typeof state.source === "string") {
        state.resolved = resolveTarget(state.source, null);
        context.target = state.resolved;
        return true;
      }
      const target = resolveTarget(state.source, context.fallbackTarget);
      if (rejectDisconnected && state.wasConnected && target && target.isConnected === false)
        return false;
      state.resolved = target;
      context.target = target;
      return true;
    }
    function createRequestContext(actionId, payload, options) {
      const config = getConfig();
      const sourceElement = options.sourceEl || null;
      const configuredSync = options.sync != null ? options.sync : sourceElement ? getAttr(sourceElement, "heimdall-sync") : null;
      const configuredGroup = options.syncGroup != null ? options.syncGroup : sourceElement ? getAttr(sourceElement, "heimdall-sync-group") : null;
      const context = {
        requestId: ++nextRequestId,
        attempt: 0,
        actionId,
        trigger: options.triggerName || null,
        sourceElement,
        target: options.target,
        fallbackTarget: options.fallbackTarget || null,
        swap: options.swap || "inner",
        endpoint: options.endpoint || config.endpoints.contentActions,
        headers: Object.assign({}, options.headers || {}),
        sync: configuredSync || config.requestSync || "parallel",
        syncGroup: configuredGroup && String(configuredGroup).trim() ? String(configuredGroup).trim() : null,
        timeoutMs: options.timeoutMs != null ? Number(options.timeoutMs) : Number(config.requestTimeoutMs || 0),
        externalSignal: options.signal || null,
        disableElement: options.disableElement || null,
        controller: null,
        signal: null,
        request: null,
        response: null,
        rawHtml: null,
        result: null,
        cancelled: false,
        cancelReason: null,
        timedOut: false,
        startedAt: performance.now(),
        startedExecutionAt: null,
        finishedAt: null
      };
      installPayloadState(context, payload, options.payloadBinding);
      return context;
    }
    function emitCancellation(context, result) {
      const detail = context;
      detail.result = result;
      if (result.cancelReason === "timeout")
        emitLifecycle(context.sourceElement, "heimdall:request-timeout", detail);
      emitLifecycle(context.sourceElement, "heimdall:request-cancel", detail);
    }
    function requestHeadersFailure(context, error) {
      context.request = null;
      context.response = null;
      context.rawHtml = null;
      const result = {
        ok: false,
        status: 0,
        code: REQUEST_HEADERS_FAILED_CODE,
        error: error.message,
        response: null,
        html: null,
        ms: Math.max(0, performance.now() - (context.startedExecutionAt || context.startedAt)),
        requestId: context.requestId
      };
      emit("heimdall:error", {
        actionId: context.actionId,
        payload: context.payload,
        target: context.target,
        swap: context.swap,
        status: 0,
        code: REQUEST_HEADERS_FAILED_CODE,
        phase: "request-headers",
        error
      });
      return result;
    }
    async function invoke(actionId, payload, options) {
      options = options || {};
      const context = createRequestContext(actionId, payload, options);
      emitLifecycle(context.sourceElement, "heimdall:request-config", context);
      finalizePayloadConfiguration(context);
      initializeTargetState(context);
      context.sync = coordinator.normalizeStrategy(context.sync, getConfig().requestSync || "parallel");
      try {
        const result = await coordinator.run(context, async () => {
          if (!refreshExecutionPayload(context)) {
            coordinator.cancel(context, "payload-source-unavailable");
            return coordinator.getCancellationResult(context);
          }
          if (!refreshTarget(context, true)) {
            coordinator.cancel(context, "target-disconnected");
            return coordinator.getCancellationResult(context);
          }
          busyState.acquire(context.disableElement);
          try {
            return await executeRequestAttempt(context, options, true);
          } finally {
            busyState.release(context.disableElement);
          }
        });
        context.result = result;
        if (result && result.cancelled) {
          emitCancellation(context, result);
        } else if (context.response) {
          emitLifecycle(context.sourceElement, "heimdall:request-after", context);
        }
        return result;
      } finally {
        context.finishedAt = performance.now();
        emitLifecycle(context.sourceElement, "heimdall:request-finally", context);
      }
    }
    async function executeRequestAttempt(context, options, shouldRetry) {
      context.attempt++;
      const url = new URL(context.endpoint, global.location?.origin || void 0);
      url.searchParams.set("action", context.actionId);
      const antiforgeryEnabled = getConfig().antiforgery !== false;
      let token = null;
      if (antiforgeryEnabled) {
        try {
          token = await ensureCsrfToken();
        } catch (error) {
          if (!coordinator.isCurrent(context))
            return coordinator.getCancellationResult(context);
          if (error?.code === REQUEST_HEADERS_FAILED_CODE)
            return requestHeadersFailure(context, error);
          throw error;
        }
        if (!coordinator.isCurrent(context))
          return coordinator.getCancellationResult(context);
      }
      const isFormData = typeof global.FormData === "function" && context.payload instanceof global.FormData;
      let headers = {
        [actionHeader]: context.actionId
      };
      if (antiforgeryEnabled)
        headers[csrfHeader] = token;
      const clientInfo = typeof getClientInfoHeader === "function" ? getClientInfoHeader(context) : null;
      if (clientInfo)
        headers[clientInfoHeader] = clientInfo;
      if (!isFormData)
        headers["Content-Type"] = "application/json";
      if (typeof resolveRequestHeaders === "function") {
        try {
          headers = await resolveRequestHeaders({
            kind: "content-action",
            url: url.toString(),
            method: "POST",
            actionId: context.actionId,
            requestId: context.requestId,
            attempt: context.attempt,
            sourceElement: context.sourceElement,
            signal: context.signal
          }, headers);
        } catch (error) {
          if (!coordinator.isCurrent(context))
            return coordinator.getCancellationResult(context);
          if (error?.code === REQUEST_HEADERS_FAILED_CODE)
            return requestHeadersFailure(context, error);
          throw error;
        }
      }
      if (!coordinator.isCurrent(context))
        return coordinator.getCancellationResult(context);
      if (typeof mergeRequestHeaders === "function")
        mergeRequestHeaders(headers, context.headers);
      else {
        for (const key in context.headers)
          headers[key] = context.headers[key];
      }
      let body = context.payload;
      if (!isFormData) {
        body = "{}";
        try {
          body = context.payload == null ? "{}" : JSON.stringify(context.payload);
        } catch (e) {
          const err = new Error(`Heimdall payload is not JSON-serializable for action '${context.actionId}'.`);
          err.cause = e;
          emit("heimdall:error", {
            actionId: context.actionId,
            payload: context.payload,
            target: context.target,
            swap: context.swap,
            status: 0,
            error: err
          });
          throw err;
        }
      }
      context.request = {
        url: url.toString(),
        headers,
        body,
        credentials: "same-origin",
        signal: context.signal
      };
      const shouldContinue = emitLifecycle(
        context.sourceElement,
        "heimdall:request-before",
        context,
        { cancelable: true }
      );
      if (!shouldContinue) {
        coordinator.cancel(context, "event-cancelled");
        return coordinator.getCancellationResult(context);
      }
      if (!coordinator.isCurrent(context))
        return coordinator.getCancellationResult(context);
      const attemptStarted = performance.now();
      emit("heimdall:before", {
        actionId: context.actionId,
        payload: context.payload,
        target: context.target,
        swap: context.swap,
        endpoint: context.request.url
      });
      dbg("invoke ->", context.actionId, {
        endpoint: context.request.url,
        swap: context.swap,
        target: context.target,
        requestId: context.requestId,
        attempt: context.attempt
      });
      let response;
      try {
        response = await global.fetch(context.request.url, {
          method: "POST",
          headers: context.request.headers,
          body: context.request.body,
          credentials: context.request.credentials,
          signal: context.request.signal || void 0
        });
      } catch (networkError) {
        if (context.cancelled || networkError && networkError.name === "AbortError" && context.signal && context.signal.aborted)
          return coordinator.getCancellationResult(context);
        const result2 = {
          ok: false,
          status: 0,
          error: networkError.message,
          response: null,
          html: null,
          ms: performance.now() - attemptStarted,
          requestId: context.requestId
        };
        emit("heimdall:error", {
          actionId: context.actionId,
          payload: context.payload,
          target: context.target,
          swap: context.swap,
          error: networkError
        });
        return result2;
      }
      const rawHtml = await safeText(response);
      const ms = performance.now() - attemptStarted;
      context.response = response;
      context.rawHtml = rawHtml;
      if (!coordinator.isCurrent(context))
        return coordinator.getCancellationResult(context);
      if (antiforgeryEnabled && response.status === 400 && shouldRetry) {
        const lower = rawHtml.toLowerCase();
        if (lower.includes("csrf") || lower.includes("antiforgery")) {
          dbg("csrf validation suspected; retrying once with fresh token");
          clearCsrfToken();
          return executeRequestAttempt(context, options, false);
        }
      }
      const authRedirectUrl = getAuthRedirectUrlFromResponse(response);
      const normalizedAuthRedirectUrl = authRedirectUrl ? normalizeFollowedAuthRedirectUrl(global, getConfig, authRedirectUrl) : null;
      const useDefaultUnauthorizedHandling = emitUnauthorized({
        response,
        emitLifecycle,
        sourceElement: context.sourceElement,
        detail: {
          kind: "content-action",
          actionId: context.actionId,
          requestId: context.requestId,
          attempt: context.attempt,
          sourceElement: context.sourceElement,
          url: context.request.url,
          method: "POST",
          body: rawHtml,
          redirectUrl: normalizedAuthRedirectUrl,
          requestContext: context
        }
      });
      if (authRedirectUrl && useDefaultUnauthorizedHandling) {
        const redirectUrl2 = normalizedAuthRedirectUrl;
        emit("heimdall:redirect", {
          actionId: context.actionId,
          payload: context.payload,
          target: context.target,
          swap: context.swap,
          endpoint: context.request.url,
          status: response.status,
          url: redirectUrl2
        });
        dbg("redirecting", { actionId: context.actionId, url: redirectUrl2 });
        global.location.href = redirectUrl2;
        return {
          ok: response.ok,
          status: response.status,
          html: null,
          error: null,
          response,
          ms,
          abortSwap: true,
          abortReason: "redirect",
          redirectUrl: redirectUrl2,
          requestId: context.requestId
        };
      }
      let html = rawHtml;
      let abortSwap = false;
      let abortReason = null;
      let redirectUrl = null;
      let jsAfter = [];
      let oobMutationTargets = [];
      let historyCommand = null;
      if (response.ok) {
        const oob = dom.processOob(html, context.sourceElement, {
          kind: "action",
          actionId: context.actionId,
          payload: context.payload,
          target: context.target,
          swap: context.swap,
          endpoint: context.request.url,
          status: response.status,
          sourceEl: context.sourceElement,
          requestContext: context
        });
        html = oob.html;
        abortSwap = !!oob.abortSwap;
        abortReason = oob.abortReason || null;
        redirectUrl = oob.redirectUrl || null;
        jsAfter = oob.jsAfter || [];
        oobMutationTargets = oob.mutationTargets || [];
        historyCommand = oob.historyCommand || null;
      } else {
        html = dom.sanitizeHtmlStringNoApply(html);
      }
      if (response.ok && redirectUrl) {
        emit("heimdall:redirect", {
          actionId: context.actionId,
          payload: context.payload,
          target: context.target,
          swap: context.swap,
          endpoint: context.request.url,
          status: response.status,
          url: redirectUrl
        });
        dbg("redirecting", { actionId: context.actionId, url: redirectUrl });
        global.location.href = redirectUrl;
        return {
          ok: true,
          status: response.status,
          html: null,
          error: null,
          response,
          ms,
          abortSwap: true,
          abortReason: "redirect",
          redirectUrl,
          requestId: context.requestId
        };
      }
      if (response.ok && abortSwap) {
        emit("heimdall:abort", {
          actionId: context.actionId,
          payload: context.payload,
          target: context.target,
          swap: context.swap,
          endpoint: context.request.url,
          status: response.status,
          reason: abortReason
        });
        dbg("swap aborted", { actionId: context.actionId, reason: abortReason, target: context.target });
      }
      if (response.ok && !abortSwap) {
        refreshTarget(context, false);
      }
      if (response.ok && context.target && !abortSwap) {
        const mainTemplate = dom.parseHtmlToTemplate(html);
        dom.stripInvocationsFromFragment(mainTemplate.content);
        dom.stripAbortsFromFragment(mainTemplate.content);
        dom.stripRedirectsFromFragment(mainTemplate.content);
        dom.stripHistoryFromFragment(mainTemplate.content);
        dom.stripJsInvokeVoidFromFragment(mainTemplate.content);
        dom.stripMutationsFromFragment(mainTemplate.content);
        const swapResult = dom.applySwap(
          context.target,
          mainTemplate.content,
          context.swap,
          {
            kind: "action",
            swapKind: "main",
            sourceEl: context.sourceElement,
            requestContext: context
          }
        );
        const { didApply, appliedRoot } = swapResult;
        if (didApply && !getConfig().observeDom) {
          try {
            boot(appliedRoot || swapResult.target || context.target);
          } catch {
          }
        }
      }
      if (response.ok)
        dom.reconcileMutations(oobMutationTargets);
      let historyResult = null;
      if (response.ok && historyCommand) {
        historyResult = historyRuntime.apply(historyCommand, context.sourceElement, {
          requestContext: context
        });
      }
      if (response.ok) {
        dom.invokeJsInvokeVoidDirectives(jsAfter, {
          phase: "after",
          kind: "action",
          actionId: context.actionId,
          payload: context.payload,
          target: context.target,
          swap: context.swap,
          endpoint: context.request.url,
          status: response.status
        });
      }
      const result = {
        ok: response.ok,
        status: response.status,
        html: response.ok ? html : null,
        error: response.ok ? null : html,
        response,
        ms,
        abortSwap,
        abortReason,
        redirectUrl,
        requestId: context.requestId
      };
      if (historyResult)
        result.history = historyResult;
      if (!response.ok) {
        emit("heimdall:error", {
          actionId: context.actionId,
          payload: context.payload,
          target: context.target,
          swap: context.swap,
          status: response.status,
          body: html
        });
      } else {
        emit("heimdall:after", {
          actionId: context.actionId,
          payload: context.payload,
          target: context.target,
          swap: context.swap,
          endpoint: context.request.url,
          status: response.status,
          ms,
          html,
          redirectUrl
        });
      }
      if (typeof options.onSuccess === "function" && response.ok)
        options.onSuccess(result);
      if (typeof options.onError === "function" && !response.ok)
        options.onError(result);
      dbg("invoke <-", context.actionId, result);
      return result;
    }
    const DEFAULT_DISABLE_BY_TRIGGER = {
      load: false,
      click: true,
      change: false,
      input: false,
      submit: true,
      keydown: false,
      blur: false,
      hover: false,
      visible: false,
      "document-visible": false,
      online: false,
      scroll: false,
      sse: false
    };
    function getCommonOptions(el, triggerName) {
      const target = getAttr(el, "heimdall-content-target") || el;
      const swap = getAttr(el, "heimdall-content-swap") || "inner";
      const sync = getAttr(el, "heimdall-sync");
      const syncGroup = getAttr(el, "heimdall-sync-group");
      let payloadResolution = payloadBindingFromElement(el);
      let payload = payloadResolution.value;
      let payloadBinding = payloadResolution.binding;
      if (payload == null && triggerName === "submit") {
        if (el && el.tagName === "FORM") {
          payload = formDataToPayload(new FormData(el));
          payloadBinding = null;
        } else {
          const form = el.closest && el.closest("form");
          if (form) {
            payload = formDataToPayload(new FormData(form));
            payloadBinding = null;
          }
        }
      }
      return { target, swap, payload, payloadBinding, sync, syncGroup };
    }
    async function runActionFromElement(el, actionId, triggerName, extraOptions) {
      if (!el || !actionId)
        return;
      if (el.hasAttribute("disabled") || el.getAttribute("aria-disabled") === "true")
        return;
      const { target, swap, payload, payloadBinding, sync, syncGroup } = getCommonOptions(el, triggerName);
      const defaultDisable = DEFAULT_DISABLE_BY_TRIGGER[triggerName] ?? false;
      const shouldDisable = truthyAttr(el, "heimdall-content-disable", defaultDisable);
      const opts = Object.assign({
        target,
        swap,
        sync,
        syncGroup,
        fallbackTarget: el,
        sourceEl: el,
        triggerName,
        payloadBinding,
        disableElement: shouldDisable ? el : null
      }, extraOptions || {});
      try {
        await invoke(actionId, payload, opts);
      } catch (err) {
        console.error(err);
      }
    }
    return {
      invoke,
      runActionFromElement
    };
  }

  // core/boot-triggers.js
  function createBootTriggers({
    global,
    getConfig,
    runActionFromElement
  }) {
    function matchesTriggerAttr(el, attr) {
      return isElement(el) && el.hasAttribute(attr);
    }
    function candidates(root, selector) {
      const result = [];
      const seen = /* @__PURE__ */ new Set();
      const add = (el) => {
        if (!isElement(el) || seen.has(el))
          return;
        seen.add(el);
        result.push(el);
      };
      add(root);
      const scope = isElement(root) ? root : document;
      for (const el of scope.querySelectorAll(selector))
        add(el);
      return result;
    }
    const _loadActions = /* @__PURE__ */ new WeakMap();
    function reconcileLoad(el) {
      const actionId = (getAttr(el, "heimdall-content-load") || "").trim();
      const previous = _loadActions.get(el) || null;
      if (!actionId) {
        _loadActions.delete(el);
        el.__heimdallLoaded = false;
        return;
      }
      if (previous === actionId)
        return;
      _loadActions.set(el, actionId);
      el.__heimdallLoaded = true;
      runActionFromElement(el, actionId, "load").catch(() => {
      });
    }
    function bootLoads(root) {
      for (const el of candidates(root, "[heimdall-content-load]"))
        reconcileLoad(el);
    }
    let _visibleObserver = null;
    const _visibleStates = /* @__PURE__ */ new WeakMap();
    function ensureVisibleObserver() {
      if (_visibleObserver)
        return _visibleObserver;
      if (!("IntersectionObserver" in global)) {
        _visibleObserver = { observe() {
        }, unobserve() {
        } };
        return _visibleObserver;
      }
      const config = getConfig();
      _visibleObserver = new IntersectionObserver((entries) => {
        for (const entry of entries) {
          if (!entry.isIntersecting)
            continue;
          const el = entry.target;
          const actionId = getAttr(el, "heimdall-content-visible");
          if (!actionId)
            continue;
          const once = truthyAttr(el, "heimdall-visible-once", true);
          if (once) {
            try {
              _visibleObserver.unobserve(el);
            } catch {
            }
            const state = _visibleStates.get(el);
            if (state)
              state.observed = false;
          }
          runActionFromElement(el, actionId, "visible").catch(() => {
          });
        }
      }, {
        root: null,
        rootMargin: config.visibleRootMargin || "0px",
        threshold: config.visibleThreshold || 0
      });
      return _visibleObserver;
    }
    function stopVisible(el) {
      const state = _visibleStates.get(el);
      if (!state)
        return;
      try {
        ensureVisibleObserver().unobserve(el);
      } catch {
      }
      _visibleStates.delete(el);
      el.__heimdallVisibleBound = false;
    }
    function reconcileVisible(el) {
      const actionId = (getAttr(el, "heimdall-content-visible") || "").trim();
      if (!actionId) {
        stopVisible(el);
        return;
      }
      const once = truthyAttr(el, "heimdall-visible-once", true);
      const previous = _visibleStates.get(el);
      if (previous && previous.actionId === actionId && previous.once === once)
        return;
      if (previous)
        stopVisible(el);
      _visibleStates.set(el, { actionId, once, observed: true });
      el.__heimdallVisibleBound = true;
      try {
        ensureVisibleObserver().observe(el);
      } catch {
      }
    }
    function bootVisible(root) {
      for (const el of candidates(root, "[heimdall-content-visible]"))
        reconcileVisible(el);
    }
    const _scrollStates = /* @__PURE__ */ new WeakMap();
    function isNearScrollEnd(el, thresholdPx) {
      const target = el === document.body || el === document.documentElement ? document.scrollingElement || document.documentElement : el;
      if (!target)
        return false;
      return target.scrollTop + target.clientHeight >= target.scrollHeight - thresholdPx;
    }
    function stopScroll(el) {
      const state = _scrollStates.get(el);
      if (!state)
        return;
      try {
        el.removeEventListener("scroll", state.handler);
      } catch {
      }
      _scrollStates.delete(el);
      el.__heimdallScrollBound = false;
    }
    function reconcileScroll(el) {
      const actionId = (getAttr(el, "heimdall-content-scroll") || "").trim();
      if (!actionId) {
        stopScroll(el);
        return;
      }
      const previous = _scrollStates.get(el);
      if (previous && previous.actionId === actionId)
        return;
      if (previous)
        stopScroll(el);
      const state = { actionId, ticking: false, lastFire: 0, handler: null };
      state.handler = () => {
        if (state.ticking)
          return;
        state.ticking = true;
        requestAnimationFrame(() => {
          state.ticking = false;
          const currentActionId = getAttr(el, "heimdall-content-scroll");
          if (!currentActionId)
            return;
          const config = getConfig();
          const threshold = intAttr(el, "heimdall-scroll-threshold", config.scrollThresholdPx || 120);
          const minInterval = config.scrollMinIntervalMs || 250;
          if (!isNearScrollEnd(el, threshold))
            return;
          const now = Date.now();
          if (now - state.lastFire < minInterval)
            return;
          state.lastFire = now;
          runActionFromElement(el, currentActionId, "scroll").catch(() => {
          });
        });
      };
      _scrollStates.set(el, state);
      el.__heimdallScrollBound = true;
      el.addEventListener("scroll", state.handler, { passive: true });
    }
    function bootScroll(root) {
      for (const el of candidates(root, "[heimdall-content-scroll]"))
        reconcileScroll(el);
    }
    const _pollStates = /* @__PURE__ */ new WeakMap();
    function stopPoll(el) {
      const state = _pollStates.get(el);
      if (!state)
        return;
      clearTimeout(state.timerId);
      _pollStates.delete(el);
      el.__heimdallPollBound = false;
    }
    function startPoll(el, actionId, intervalMs) {
      const state = { actionId, intervalMs, timerId: null, inFlight: false };
      _pollStates.set(el, state);
      el.__heimdallPollBound = true;
      const tick = async () => {
        if (!el.isConnected) {
          stopPoll(el);
          return;
        }
        if (document.hidden || state.inFlight)
          return;
        state.inFlight = true;
        try {
          await runActionFromElement(el, state.actionId, "load", { reason: "poll" });
        } finally {
          state.inFlight = false;
        }
      };
      const schedule = () => {
        if (!el.isConnected || _pollStates.get(el) !== state) {
          stopPoll(el);
          return;
        }
        clearTimeout(state.timerId);
        state.timerId = setTimeout(async () => {
          try {
            await tick();
          } catch {
          } finally {
            if (_pollStates.get(el) === state)
              schedule();
          }
        }, state.intervalMs);
      };
      schedule();
    }
    function reconcilePoll(el) {
      const intervalMs = intAttr(el, "heimdall-poll", 0);
      const actionId = (getAttr(el, "heimdall-content-load") || "").trim();
      const previous = _pollStates.get(el);
      if (!intervalMs || intervalMs <= 0 || !actionId) {
        if (intervalMs > 0 && !actionId) {
          console.warn(`[Heimdall] heimdall-poll set but no heimdall-content-load found on element.`, el);
        }
        stopPoll(el);
        return;
      }
      if (previous && previous.intervalMs === intervalMs && previous.actionId === actionId)
        return;
      if (previous)
        stopPoll(el);
      startPoll(el, actionId, intervalMs);
    }
    function bootPoll(root) {
      for (const el of candidates(root, "[heimdall-poll]"))
        reconcilePoll(el);
    }
    return {
      bootLoads,
      bootPoll,
      bootScroll,
      bootVisible,
      matchesTriggerAttr
    };
  }

  // core/busy-state.js
  function createBusyStateManager() {
    const states = /* @__PURE__ */ new WeakMap();
    function acquire(el) {
      if (!el)
        return;
      let state = states.get(el);
      if (!state) {
        state = {
          count: 0,
          hadDisabled: false,
          hadAriaBusy: false,
          ariaBusy: null
        };
        states.set(el, state);
      }
      if (state.count === 0) {
        state.hadDisabled = el.hasAttribute("disabled");
        state.hadAriaBusy = el.hasAttribute("aria-busy");
        state.ariaBusy = el.getAttribute("aria-busy");
      }
      state.count++;
      el.setAttribute("disabled", "disabled");
      el.setAttribute("aria-busy", "true");
    }
    function release(el) {
      if (!el)
        return;
      const state = states.get(el);
      if (!state)
        return;
      state.count = Math.max(0, state.count - 1);
      if (state.count > 0)
        return;
      if (state.hadDisabled)
        el.setAttribute("disabled", "disabled");
      else
        el.removeAttribute("disabled");
      if (state.hadAriaBusy)
        el.setAttribute("aria-busy", state.ariaBusy == null ? "" : state.ariaBusy);
      else
        el.removeAttribute("aria-busy");
      states.delete(el);
    }
    function rebaseAttribute(el, name, present, value) {
      const state = el ? states.get(el) : null;
      if (!state || state.count <= 0)
        return;
      const normalized = String(name || "").toLowerCase();
      if (normalized === "disabled") {
        state.hadDisabled = !!present;
        el.setAttribute("disabled", "disabled");
        return;
      }
      if (normalized === "aria-busy") {
        state.hadAriaBusy = !!present;
        state.ariaBusy = present ? String(value == null ? "" : value) : null;
        el.setAttribute("aria-busy", "true");
      }
    }
    return {
      acquire,
      rebaseAttribute,
      release
    };
  }

  // core/client-info.js
  var MEDIA_QUERIES = [
    "(prefers-color-scheme: dark)",
    "(prefers-color-scheme: light)",
    "(prefers-reduced-motion: reduce)",
    "(prefers-contrast: more)",
    "(prefers-contrast: less)",
    "(prefers-contrast: custom)",
    "(forced-colors: active)",
    "(pointer: coarse)",
    "(pointer: fine)",
    "(hover: hover)"
  ];
  function createClientInfoRuntime({ global, getConfig, emitLifecycle, dbg }) {
    const media = /* @__PURE__ */ new Map();
    let installed = false;
    let dirty = true;
    let cachedInfo = null;
    let capturedAt = 0;
    function markDirty() {
      dirty = true;
    }
    function getMediaQuery(query) {
      if (media.has(query))
        return media.get(query);
      let result = null;
      try {
        result = typeof global.matchMedia === "function" ? global.matchMedia(query) : null;
      } catch {
        result = null;
      }
      media.set(query, result);
      return result;
    }
    function matches(query) {
      return getMediaQuery(query)?.matches === true;
    }
    function listen(target, eventName) {
      if (target && typeof target.addEventListener === "function")
        target.addEventListener(eventName, markDirty, { passive: true });
    }
    function installInvalidationListeners() {
      if (installed)
        return;
      installed = true;
      for (const eventName of ["resize", "orientationchange", "languagechange", "online", "offline"])
        listen(global, eventName);
      listen(global.visualViewport, "resize");
      listen(global.screen?.orientation, "change");
      for (const query of MEDIA_QUERIES) {
        const result = getMediaQuery(query);
        if (!result)
          continue;
        if (typeof result.addEventListener === "function")
          result.addEventListener("change", markDirty);
        else if (typeof result.addListener === "function")
          result.addListener(markDirty);
      }
    }
    function finiteNumber(value, fallback = 0) {
      const number = Number(value);
      return Number.isFinite(number) ? number : fallback;
    }
    function wholeNumber(value, fallback = 0) {
      return Math.max(0, Math.round(finiteNumber(value, fallback)));
    }
    function shortString(value, maxLength) {
      return String(value || "").trim().slice(0, maxLength);
    }
    function preferredContrast() {
      if (matches("(prefers-contrast: more)")) return "more";
      if (matches("(prefers-contrast: less)")) return "less";
      if (matches("(prefers-contrast: custom)")) return "custom";
      return "no-preference";
    }
    function colorScheme() {
      if (matches("(prefers-color-scheme: dark)")) return "dark";
      if (matches("(prefers-color-scheme: light)")) return "light";
      return "no-preference";
    }
    function collect() {
      const navigator = global.navigator || {};
      const screen = global.screen || {};
      const viewport = global.visualViewport;
      const viewportWidth = finiteNumber(viewport?.width, finiteNumber(global.innerWidth));
      const viewportHeight = finiteNumber(viewport?.height, finiteNumber(global.innerHeight));
      const screenWidth = wholeNumber(screen.width, viewportWidth);
      const screenHeight = wholeNumber(screen.height, viewportHeight);
      const maxTouchPoints = wholeNumber(navigator.maxTouchPoints);
      const touch = maxTouchPoints > 0 || "ontouchstart" in global;
      const coarsePointer = matches("(pointer: coarse)");
      const pointer = coarsePointer ? "coarse" : matches("(pointer: fine)") ? "fine" : "none";
      const shortSide = Math.min(
        screenWidth || wholeNumber(viewportWidth),
        screenHeight || wholeNumber(viewportHeight)
      );
      const deviceCategory = touch && coarsePointer ? shortSide < 600 ? "mobile" : shortSide < 1024 ? "tablet" : "desktop" : "desktop";
      let timeZone = "";
      try {
        timeZone = shortString(global.Intl?.DateTimeFormat().resolvedOptions().timeZone, 128);
      } catch {
        timeZone = "";
      }
      const languages = Array.isArray(navigator.languages) ? navigator.languages : [navigator.language];
      return {
        timeZone: timeZone || null,
        utcOffsetMinutes: -(/* @__PURE__ */ new Date()).getTimezoneOffset(),
        locale: shortString(navigator.language, 64) || null,
        languages: languages.map((language) => shortString(language, 64)).filter(Boolean).slice(0, 16),
        viewportWidth,
        viewportHeight,
        screenWidth,
        screenHeight,
        devicePixelRatio: finiteNumber(global.devicePixelRatio, 1),
        orientation: viewportHeight >= viewportWidth ? "portrait" : "landscape",
        deviceCategory,
        colorScheme: colorScheme(),
        prefersReducedMotion: matches("(prefers-reduced-motion: reduce)"),
        prefersContrast: preferredContrast(),
        forcedColors: matches("(forced-colors: active)"),
        touch,
        maxTouchPoints,
        pointer,
        hover: matches("(hover: hover)"),
        online: navigator.onLine !== false
      };
    }
    function cloneInfo(info) {
      return {
        ...info,
        languages: Array.isArray(info.languages) ? [...info.languages] : []
      };
    }
    function getHeaderValue(requestContext) {
      const config = getConfig();
      if (!config || config.clientInfo !== true)
        return null;
      installInvalidationListeners();
      const configuredMaxAge = Number(config.clientInfoMaxAgeMs);
      const maxAge = Number.isFinite(configuredMaxAge) ? Math.max(0, configuredMaxAge) : 6e4;
      const now = Date.now();
      const age = now - capturedAt;
      if (!cachedInfo || dirty || age < 0 || age >= maxAge) {
        try {
          cachedInfo = collect();
          capturedAt = now;
          dirty = false;
        } catch (error) {
          cachedInfo = null;
          if (typeof dbg === "function")
            dbg("client information collection failed", error);
        }
      }
      if (!cachedInfo)
        return null;
      const detail = {
        actionId: requestContext?.actionId || null,
        requestId: requestContext?.requestId || null,
        attempt: requestContext?.attempt || 0,
        sourceElement: requestContext?.sourceElement || null,
        info: cloneInfo(cachedInfo)
      };
      const shouldInclude = typeof emitLifecycle !== "function" || emitLifecycle(
        requestContext?.sourceElement || null,
        "heimdall:client-info-before",
        detail,
        { cancelable: true }
      );
      if (!shouldInclude || detail.info == null)
        return null;
      try {
        return JSON.stringify(detail.info);
      } catch (error) {
        if (typeof dbg === "function")
          dbg("client information serialization failed", error);
        return null;
      }
    }
    return {
      getHeaderValue,
      invalidate: markDirty
    };
  }

  // core/diagnostics.js
  function createDiagnostics(getConfig) {
    function emit(name, detail) {
      try {
        document.dispatchEvent(new CustomEvent(name, { detail }));
      } catch {
      }
    }
    function emitLifecycle(source, name, detail, options) {
      options = options || {};
      try {
        const sourceIsConnected = source && source.isConnected !== false && typeof source.dispatchEvent === "function";
        const target = sourceIsConnected ? source : document;
        const event = new CustomEvent(name, {
          detail,
          bubbles: target !== document,
          composed: target !== document,
          cancelable: !!options.cancelable
        });
        return target.dispatchEvent(event);
      } catch {
        return true;
      }
    }
    function dbg(...args) {
      const config = getConfig();
      if (config && config.debug) {
        console.debug(`[Heimdall]`, ...args);
      }
    }
    return {
      emit,
      emitLifecycle,
      dbg
    };
  }

  // core/dom.js
  function createDomPipeline({ getConfig, boot, dbg, emitLifecycle, jsInvokeVoid, timeLocalization, mutations }) {
    function stripScripts(rootNode) {
      if (!rootNode || !rootNode.querySelectorAll)
        return;
      const scripts = rootNode.querySelectorAll("script");
      for (const s of scripts)
        s.remove();
    }
    function containsTag(html, tagName) {
      if (!html)
        return false;
      return html.toLowerCase().indexOf("<" + tagName.toLowerCase()) !== -1;
    }
    function parseHtmlToTemplate(html) {
      const tpl = document.createElement("template");
      tpl.innerHTML = html || "";
      stripScripts(tpl.content);
      return tpl;
    }
    function fragmentToNodesArray(fragment) {
      return Array.from(fragment.childNodes || []);
    }
    function applySwap(targetEl, fragment, swap, lifecycleContext) {
      lifecycleContext = lifecycleContext || {};
      let mode = String(swap || "inner").toLowerCase();
      if (mode === "none")
        return { didApply: false, appliedRoot: null, target: targetEl, swap: mode, cancelled: false };
      if (!targetEl)
        return { didApply: false, appliedRoot: null, target: null, swap: mode, cancelled: false };
      stripScripts(fragment);
      const detail = {
        origin: lifecycleContext.kind || "action",
        kind: lifecycleContext.swapKind || "main",
        target: targetEl,
        fragment,
        swap: mode,
        sourceElement: lifecycleContext.sourceEl || null,
        requestContext: lifecycleContext.requestContext || null
      };
      if (emitLifecycle && !emitLifecycle(
        detail.sourceElement,
        "heimdall:swap-before",
        detail,
        { cancelable: true }
      )) {
        return { didApply: false, appliedRoot: null, target: targetEl, swap: mode, cancelled: true };
      }
      targetEl = resolveTarget(detail.target, targetEl);
      fragment = detail.fragment && detail.fragment.childNodes ? detail.fragment : fragment;
      mode = String(detail.swap || mode).toLowerCase();
      if (mode === "none" || !targetEl)
        return { didApply: false, appliedRoot: null, target: targetEl, swap: mode, cancelled: false };
      stripScripts(fragment);
      if (timeLocalization && typeof timeLocalization.localize === "function") {
        const contextElement = mode === "outer" ? targetEl.parentElement : targetEl;
        timeLocalization.localize(fragment, {
          origin: detail.origin,
          kind: detail.kind,
          contextElement
        });
      }
      const nodes = fragmentToNodesArray(fragment);
      const firstElement = nodes.find((n) => n && n.nodeType === 1) || null;
      const appliedRoot = firstElement || targetEl;
      let result;
      switch (mode) {
        case "outer": {
          if (nodes.length === 0) {
            const parent = targetEl.parentElement;
            targetEl.remove();
            result = { didApply: true, appliedRoot: parent || null, target: targetEl, swap: mode, cancelled: false };
            break;
          }
          targetEl.replaceWith(...nodes);
          result = { didApply: true, appliedRoot, target: targetEl, swap: mode, cancelled: false };
          break;
        }
        case "beforeend":
          targetEl.append(...nodes);
          result = { didApply: true, appliedRoot, target: targetEl, swap: mode, cancelled: false };
          break;
        case "afterbegin":
          targetEl.prepend(...nodes);
          result = { didApply: true, appliedRoot, target: targetEl, swap: mode, cancelled: false };
          break;
        default:
          targetEl.replaceChildren(...nodes);
          result = { didApply: true, appliedRoot, target: targetEl, swap: mode, cancelled: false };
          break;
      }
      if (emitLifecycle) {
        emitLifecycle(detail.sourceElement, "heimdall:swap-after", {
          ...detail,
          target: targetEl,
          swap: mode,
          nodes,
          appliedRoot: result.appliedRoot
        });
      }
      return result;
    }
    function stripInvocationsFromFragment(fragment) {
      if (!fragment || !fragment.querySelectorAll)
        return;
      const invs = fragment.querySelectorAll("invocation");
      for (const inv of invs)
        inv.remove();
    }
    function stripMutationsFromFragment(fragment) {
      if (!fragment || !fragment.querySelectorAll)
        return;
      const directives = fragment.querySelectorAll("mutation");
      for (const directive of directives)
        directive.remove();
    }
    function stripAbortsFromFragment(fragment) {
      if (!fragment || !fragment.querySelectorAll)
        return;
      const aborts = fragment.querySelectorAll("abort");
      for (const abortEl of aborts)
        abortEl.remove();
    }
    function stripRedirectsFromFragment(fragment) {
      if (!fragment || !fragment.querySelectorAll)
        return;
      const redirects = fragment.querySelectorAll("redirect");
      for (const redirectEl of redirects)
        redirectEl.remove();
    }
    function stripHistoryFromFragment(fragment) {
      if (!fragment || !fragment.querySelectorAll)
        return;
      const directives = fragment.querySelectorAll("history");
      for (const directive of directives)
        directive.remove();
    }
    function extractHistoryFromFragment(fragment) {
      if (!fragment || !fragment.querySelectorAll)
        return null;
      const directives = Array.from(fragment.querySelectorAll("history"));
      if (directives.length === 0)
        return null;
      if (directives.length > 1) {
        stripHistoryFromFragment(fragment);
        return { error: "A Heimdall response can contain only one history directive." };
      }
      const directive = directives[0];
      const command = {
        mode: getAttr(directive, "mode"),
        url: getAttr(directive, "url") || (directive.textContent || "").trim()
      };
      directive.remove();
      return command;
    }
    function normalizeJsInvokeTiming(value) {
      return jsInvokeVoid && typeof jsInvokeVoid.normalizeTiming === "function" ? jsInvokeVoid.normalizeTiming(value) : String(value || "after").toLowerCase().trim() === "before" ? "before" : "after";
    }
    function collectJsInvokeVoidDirectives(rootNode) {
      const directives = [];
      function visit(node) {
        if (!node || !node.querySelectorAll)
          return;
        const jsEls = [];
        if (isElement(node) && String(node.localName || "").toLowerCase() === "javascript")
          jsEls.push(node);
        for (const jsEl of Array.from(node.querySelectorAll("javascript")))
          jsEls.push(jsEl);
        for (const jsEl of jsEls) {
          directives.push({
            functionPath: getAttr(jsEl, "function"),
            argsJson: getAttr(jsEl, "args"),
            timing: normalizeJsInvokeTiming(getAttr(jsEl, "timing")),
            sourceEl: jsEl
          });
          jsEl.remove();
        }
        for (const tpl of Array.from(node.querySelectorAll("template"))) {
          if (tpl.content)
            visit(tpl.content);
        }
      }
      visit(rootNode);
      return directives;
    }
    function stripJsInvokeVoidFromFragment(fragment) {
      collectJsInvokeVoidDirectives(fragment);
    }
    function splitJsInvokeVoidDirectives(directives) {
      const before = [];
      const after = [];
      for (const directive of directives || []) {
        if (normalizeJsInvokeTiming(directive.timing) === "before")
          before.push(directive);
        else
          after.push(directive);
      }
      return { before, after };
    }
    function invokeJsInvokeVoidDirectives(directives, context) {
      if (!jsInvokeVoid || typeof jsInvokeVoid.invokeAll !== "function")
        return 0;
      return jsInvokeVoid.invokeAll(directives, context);
    }
    function extractRedirectFromFragment(fragment) {
      if (!fragment || !fragment.querySelector)
        return null;
      const redirectEl = fragment.querySelector("redirect");
      if (!redirectEl)
        return null;
      const urlAttr = getAttr(redirectEl, "url");
      if (urlAttr && urlAttr.trim())
        return { url: urlAttr.trim() };
      const textUrl = (redirectEl.textContent || "").trim();
      if (textUrl)
        return { url: textUrl };
      return null;
    }
    function fragmentToHtml(fragment) {
      const host = document.createElement("div");
      try {
        host.append(fragment.cloneNode(true));
      } catch {
        const frag = fragment && fragment.cloneNode ? fragment.cloneNode(true) : null;
        if (frag && frag.childNodes)
          host.append(...Array.from(frag.childNodes));
      }
      return host.innerHTML;
    }
    function sanitizeHtmlStringNoApply(html) {
      if (!html)
        return html;
      const hasScript = containsTag(html, "script");
      const hasInv = containsTag(html, "invocation");
      const hasAbort = containsTag(html, "abort");
      const hasRedirect = containsTag(html, "redirect");
      const hasJsInvokeVoid = containsTag(html, "javascript");
      const hasMutation = containsTag(html, "mutation");
      const hasHistory = containsTag(html, "history");
      if (!hasScript && !hasInv && !hasAbort && !hasRedirect && !hasJsInvokeVoid && !hasMutation && !hasHistory)
        return html;
      const tpl = parseHtmlToTemplate(html);
      stripInvocationsFromFragment(tpl.content);
      stripAbortsFromFragment(tpl.content);
      stripRedirectsFromFragment(tpl.content);
      stripJsInvokeVoidFromFragment(tpl.content);
      stripMutationsFromFragment(tpl.content);
      stripHistoryFromFragment(tpl.content);
      return fragmentToHtml(tpl.content);
    }
    function processOob(html, sourceEl, context) {
      const hasScript = containsTag(html, "script");
      const hasInv = containsTag(html, "invocation");
      const hasAbort = containsTag(html, "abort");
      const hasRedirect = containsTag(html, "redirect");
      const hasJsInvokeVoid = containsTag(html, "javascript");
      const hasMutation = containsTag(html, "mutation");
      const hasHistory = containsTag(html, "history");
      if (!hasInv && !hasScript && !hasAbort && !hasRedirect && !hasJsInvokeVoid && !hasMutation && !hasHistory) {
        return {
          html: html || "",
          applied: 0,
          abortSwap: false,
          abortReason: null,
          redirectUrl: null,
          jsAfter: [],
          mutationTargets: [],
          historyCommand: null
        };
      }
      const tpl = parseHtmlToTemplate(html);
      const fragment = tpl.content;
      const redirect = extractRedirectFromFragment(fragment);
      if (redirect && redirect.url) {
        stripRedirectsFromFragment(fragment);
        stripHistoryFromFragment(fragment);
        return {
          html: fragmentToHtml(fragment),
          applied: 0,
          abortSwap: true,
          abortReason: "redirect",
          redirectUrl: redirect.url,
          jsAfter: [],
          mutationTargets: [],
          historyCommand: null
        };
      }
      const historyCommand = extractHistoryFromFragment(fragment);
      const jsDirectives = collectJsInvokeVoidDirectives(fragment);
      const jsGroups = splitJsInvokeVoidDirectives(jsDirectives);
      invokeJsInvokeVoidDirectives(jsGroups.before, Object.assign({ phase: "before", sourceEl }, context || {}));
      const aborts = fragment.querySelectorAll("abort");
      let abortSwap = false;
      let abortReason = null;
      if (aborts && aborts.length > 0) {
        abortSwap = true;
        for (const abortEl of Array.from(aborts)) {
          const reason = getAttr(abortEl, "reason");
          if (abortReason == null && reason && reason.trim())
            abortReason = reason.trim();
          abortEl.remove();
        }
      }
      const commands = fragment.querySelectorAll("invocation,mutation");
      if (!commands || commands.length === 0) {
        return {
          html: fragmentToHtml(fragment),
          applied: 0,
          abortSwap,
          abortReason,
          redirectUrl: null,
          jsAfter: jsGroups.after,
          mutationTargets: [],
          historyCommand
        };
      }
      if (!getConfig().oobEnabled) {
        stripInvocationsFromFragment(fragment);
        stripMutationsFromFragment(fragment);
        return {
          html: fragmentToHtml(fragment),
          applied: 0,
          abortSwap,
          abortReason,
          redirectUrl: null,
          jsAfter: jsGroups.after,
          mutationTargets: [],
          historyCommand
        };
      }
      let applied = 0;
      const mutationTargets = [];
      for (const commandEl of Array.from(commands)) {
        if (String(commandEl.localName || "").toLowerCase() === "mutation") {
          const result = mutations.apply(commandEl, sourceEl, context || {});
          if (result && result.applied) {
            applied++;
            mutationTargets.push(...result.reconcileTargets || []);
          }
          commandEl.remove();
          continue;
        }
        const invEl = commandEl;
        const targetSel = getAttr(invEl, "heimdall-content-target");
        if (!targetSel) {
          dbg("Invocation missing heimdall-content-target; stripping", invEl);
          invEl.remove();
          continue;
        }
        const swap = (getAttr(invEl, "heimdall-content-swap") || "inner").toLowerCase();
        const targetEl = resolveTarget(targetSel, null);
        if (!targetEl) {
          dbg("Invocation target not found; stripping", targetSel);
          invEl.remove();
          continue;
        }
        if (swap !== "none") {
          const payloadTemplate = invEl.querySelector("template");
          let payloadFrag;
          if (payloadTemplate && payloadTemplate.content) {
            payloadFrag = payloadTemplate.content.cloneNode(true);
          } else {
            payloadFrag = parseHtmlToTemplate(invEl.innerHTML || "").content;
          }
          stripScripts(payloadFrag);
          stripInvocationsFromFragment(payloadFrag);
          stripMutationsFromFragment(payloadFrag);
          stripHistoryFromFragment(payloadFrag);
          const swapResult = applySwap(targetEl, payloadFrag, swap, Object.assign({}, context || {}, {
            sourceEl,
            swapKind: "invocation"
          }));
          const { didApply, appliedRoot } = swapResult;
          if (didApply) {
            applied++;
            if (!getConfig().observeDom) {
              try {
                boot(appliedRoot || swapResult.target || targetEl);
              } catch {
              }
            }
          }
        }
        invEl.remove();
      }
      return {
        html: fragmentToHtml(fragment),
        applied,
        abortSwap,
        abortReason,
        redirectUrl: null,
        jsAfter: jsGroups.after,
        mutationTargets,
        historyCommand
      };
    }
    function reconcileMutations(targets) {
      if (mutations && typeof mutations.reconcile === "function")
        mutations.reconcile(targets);
    }
    return {
      applySwap,
      parseHtmlToTemplate,
      processOob,
      reconcileMutations,
      sanitizeHtmlStringNoApply,
      invokeJsInvokeVoidDirectives,
      stripAbortsFromFragment,
      stripInvocationsFromFragment,
      stripMutationsFromFragment,
      stripJsInvokeVoidFromFragment,
      stripRedirectsFromFragment,
      stripHistoryFromFragment
    };
  }

  // core/event-delegates.js
  function createEventDelegates({
    getConfig,
    runActionFromElement
  }) {
    function parseTokenList(value) {
      return String(value || "").split(/\s+/).map((x) => x.trim().toLowerCase()).filter(Boolean);
    }
    function elementIgnoresTrigger(el, triggerName) {
      if (!el || !el.getAttribute)
        return false;
      const raw = getAttr(el, "heimdall-ignore");
      if (!raw)
        return false;
      const tokens = parseTokenList(raw);
      if (tokens.length === 0)
        return false;
      const trigger = String(triggerName || "").toLowerCase();
      return tokens.includes("*") || tokens.includes(trigger);
    }
    function getClosestIgnoreBoundary(target, triggerName) {
      let cur = target;
      while (cur && cur.nodeType === 1) {
        if (elementIgnoresTrigger(cur, triggerName))
          return cur;
        cur = cur.parentElement;
      }
      return null;
    }
    function matchesScope(actionEl, eventTarget) {
      const scope = (getAttr(actionEl, "heimdall-scope") || "closest").toLowerCase().trim();
      switch (scope) {
        case "self":
          return eventTarget === actionEl;
        case "closest":
        default:
          return true;
      }
    }
    function resolveActionElement(target, triggerAttr, triggerName) {
      if (!target || !target.closest)
        return null;
      const actionEl = target.closest(`[${triggerAttr}]`);
      if (!actionEl)
        return null;
      const ignoreBoundary = getClosestIgnoreBoundary(target, triggerName);
      if (!ignoreBoundary)
        return matchesScope(actionEl, target) ? actionEl : null;
      if (ignoreBoundary.contains(actionEl))
        return matchesScope(actionEl, target) ? actionEl : null;
      return null;
    }
    async function handleClick(e) {
      if (e.defaultPrevented)
        return;
      if (e.button != null && e.button !== 0)
        return;
      if (e.metaKey || e.ctrlKey || e.shiftKey || e.altKey)
        return;
      const el = resolveActionElement(e.target, "heimdall-content-click", "click");
      if (!el)
        return;
      const actionId = getAttr(el, "heimdall-content-click");
      if (!actionId)
        return;
      const isAnchor = el.tagName === "A";
      const preventDefault = truthyAttr(el, "heimdall-prevent-default", isAnchor);
      if (preventDefault)
        e.preventDefault();
      await runActionFromElement(el, actionId, "click");
    }
    async function handleChange(e) {
      if (e.defaultPrevented)
        return;
      const el = resolveActionElement(e.target, "heimdall-content-change", "change");
      if (!el)
        return;
      const actionId = getAttr(el, "heimdall-content-change");
      if (!actionId)
        return;
      const ms = intAttr(el, "heimdall-debounce", 0);
      if (ms > 0) {
        scheduleDebounced(el, "change", ms, () => {
          runActionFromElement(el, actionId, "change").catch(() => {
          });
        });
        return;
      }
      await runActionFromElement(el, actionId, "change");
    }
    const _debouncers = /* @__PURE__ */ new WeakMap();
    function scheduleDebounced(el, key, ms, fn) {
      let map = _debouncers.get(el);
      if (!map) {
        map = /* @__PURE__ */ new Map();
        _debouncers.set(el, map);
      }
      const prev = map.get(key);
      if (prev) clearTimeout(prev);
      const tid = setTimeout(() => {
        map.delete(key);
        fn();
      }, ms);
      map.set(key, tid);
    }
    async function handleInput(e) {
      if (e.defaultPrevented)
        return;
      const el = resolveActionElement(e.target, "heimdall-content-input", "input");
      if (!el)
        return;
      const actionId = getAttr(el, "heimdall-content-input");
      if (!actionId)
        return;
      const ms = intAttr(el, "heimdall-debounce", getConfig().inputDebounceMs || 250);
      if (ms > 0) {
        scheduleDebounced(el, "input", ms, () => {
          runActionFromElement(el, actionId, "input").catch(() => {
          });
        });
        return;
      }
      await runActionFromElement(el, actionId, "input");
    }
    async function handleSubmit(e) {
      if (e.defaultPrevented)
        return;
      const form = resolveActionElement(e.target, "heimdall-content-submit", "submit");
      if (!form)
        return;
      const actionId = getAttr(form, "heimdall-content-submit");
      if (!actionId)
        return;
      const preventDefault = truthyAttr(form, "heimdall-prevent-default", true);
      if (preventDefault)
        e.preventDefault();
      await runActionFromElement(form, actionId, "submit");
    }
    function normalizeKeySpec(spec) {
      return String(spec || "").trim();
    }
    function matchesKeySpec(e, spec) {
      const s = normalizeKeySpec(spec);
      if (!s)
        return true;
      if (/^\d+$/.test(s)) {
        const code = parseInt(s, 10);
        const kc = e.keyCode != null ? e.keyCode : e.which;
        return kc === code;
      }
      return String(e.key || "").toLowerCase() === s.toLowerCase();
    }
    async function handleKeydown(e) {
      if (e.defaultPrevented)
        return;
      const el = resolveActionElement(e.target, "heimdall-content-keydown", "keydown");
      if (!el)
        return;
      const actionId = getAttr(el, "heimdall-content-keydown");
      if (!actionId)
        return;
      const keySpec = getAttr(el, "heimdall-key");
      if (keySpec && !matchesKeySpec(e, keySpec))
        return;
      const wantsPreventDefault = truthyAttr(
        el,
        "heimdall-prevent-default",
        String(keySpec || "").toLowerCase() === "enter" && (e.target && (e.target.tagName === "INPUT" || e.target.tagName === "TEXTAREA"))
      );
      if (wantsPreventDefault)
        e.preventDefault();
      await runActionFromElement(el, actionId, "keydown");
    }
    async function handleFocusOut(e) {
      if (e.defaultPrevented)
        return;
      const el = resolveActionElement(e.target, "heimdall-content-blur", "blur");
      if (!el)
        return;
      const actionId = getAttr(el, "heimdall-content-blur");
      if (!actionId)
        return;
      await runActionFromElement(el, actionId, "blur");
    }
    const _hoverTimers = /* @__PURE__ */ new WeakMap();
    function isRealMouseEnter(e, el) {
      const from = e.relatedTarget;
      return !(from && (from === el || el.contains && el.contains(from)));
    }
    async function handleMouseOver(e) {
      if (e.defaultPrevented)
        return;
      const el = resolveActionElement(e.target, "heimdall-content-hover", "hover");
      if (!el)
        return;
      if (!isRealMouseEnter(e, el))
        return;
      const actionId = getAttr(el, "heimdall-content-hover");
      if (!actionId)
        return;
      const delay = intAttr(el, "heimdall-hover-delay", getConfig().hoverDelayMs || 150);
      const prev = _hoverTimers.get(el);
      if (prev)
        clearTimeout(prev);
      if (delay > 0) {
        const tid = setTimeout(() => {
          _hoverTimers.delete(el);
          runActionFromElement(el, actionId, "hover").catch(() => {
          });
        }, delay);
        _hoverTimers.set(el, tid);
        return;
      }
      await runActionFromElement(el, actionId, "hover");
    }
    function handleMouseOut(e) {
      const el = resolveActionElement(e.target, "heimdall-content-hover", "hover");
      if (!el)
        return;
      const to = e.relatedTarget;
      if (to && (to === el || el.contains && el.contains(to)))
        return;
      const tid = _hoverTimers.get(el);
      if (tid) {
        clearTimeout(tid);
        _hoverTimers.delete(el);
      }
    }
    return {
      handleChange,
      handleClick,
      handleFocusOut,
      handleInput,
      handleKeydown,
      handleMouseOut,
      handleMouseOver,
      handleSubmit
    };
  }

  // core/history.js
  var HISTORY_STATE_KEY = "__heimdall_history_v1";
  function createHistoryRuntime({ global, emitLifecycle, dbg }) {
    function currentUrl() {
      return `${global.location.pathname}${global.location.search}${global.location.hash}`;
    }
    function normalizeMode(value) {
      const mode = String(value || "").trim().toLowerCase();
      if (mode !== "push" && mode !== "replace")
        throw new Error(`Unsupported Heimdall history mode '${mode || "(empty)"}'.`);
      return mode;
    }
    function normalizeUrl(value) {
      const raw = String(value || "").trim();
      if (!raw)
        throw new Error("Heimdall history URL is required.");
      if (raw.includes("\\"))
        throw new Error("Heimdall history URLs cannot contain backslashes.");
      if (raw.startsWith("//"))
        throw new Error("Protocol-relative Heimdall history URLs are not allowed.");
      let candidate;
      if (raw.startsWith("?") || raw.startsWith("#")) {
        candidate = new URL(raw, global.location.href);
      } else if (/^[a-z][a-z\d+.-]*:/i.test(raw)) {
        candidate = new URL(raw);
      } else {
        candidate = new URL(raw.startsWith("/") ? raw : `/${raw}`, global.location.origin);
      }
      if (candidate.origin !== global.location.origin)
        throw new Error("Heimdall history URLs must be same-origin.");
      return `${candidate.pathname}${candidate.search}${candidate.hash}`;
    }
    function markState(value) {
      if (value && typeof value === "object" && value[HISTORY_STATE_KEY] === true)
        return value;
      if (value && Object.prototype.toString.call(value) === "[object Object]") {
        return Object.assign({}, value, { [HISTORY_STATE_KEY]: true });
      }
      return {
        [HISTORY_STATE_KEY]: true,
        previousState: value == null ? null : value
      };
    }
    function emitError(command, sourceElement, context, error) {
      const detail = {
        mode: command && command.mode != null ? String(command.mode) : null,
        url: command && command.url != null ? String(command.url) : null,
        error,
        sourceElement: sourceElement || null,
        requestContext: context && context.requestContext ? context.requestContext : null
      };
      emitLifecycle(sourceElement, "heimdall:history-error", detail);
      dbg("history directive ignored", detail);
    }
    function apply(command, sourceElement, context) {
      if (!command)
        return { applied: false, cancelled: false, mode: null, url: null, error: null };
      if (command.error) {
        const error = new Error(command.error);
        emitError(command, sourceElement, context, error);
        return { applied: false, cancelled: false, mode: null, url: null, error };
      }
      const detail = {
        mode: command.mode,
        url: command.url,
        sourceElement: sourceElement || null,
        requestContext: context && context.requestContext ? context.requestContext : null
      };
      if (!emitLifecycle(sourceElement, "heimdall:history-before", detail, { cancelable: true }))
        return { applied: false, cancelled: true, mode: detail.mode, url: detail.url, error: null };
      try {
        const mode = normalizeMode(detail.mode);
        const url = normalizeUrl(detail.url);
        if (mode === "push") {
          global.history.replaceState(markState(global.history.state), "", currentUrl());
          global.history.pushState(markState(null), "", url);
        } else {
          global.history.replaceState(markState(global.history.state), "", url);
        }
        const afterDetail = Object.assign({}, detail, { mode, url });
        emitLifecycle(sourceElement, "heimdall:history-after", afterDetail);
        return { applied: true, cancelled: false, mode, url, error: null };
      } catch (error) {
        emitError(detail, sourceElement, context, error);
        return { applied: false, cancelled: false, mode: null, url: null, error };
      }
    }
    global.addEventListener("popstate", (event) => {
      if (!event.state || event.state[HISTORY_STATE_KEY] !== true)
        return;
      const detail = { url: currentUrl(), state: event.state };
      if (!emitLifecycle(global.document, "heimdall:history-pop", detail, { cancelable: true }))
        return;
      global.location.reload();
    });
    return { apply, normalizeUrl };
  }

  // core/js-invoke-void.js
  function createJsInvokeVoidRuntime({ global, emit, dbg, getConfig }) {
    const validSegment = /^[A-Za-z_$][A-Za-z0-9_$]*$/;
    function normalizeTiming(value) {
      return String(value || "after").toLowerCase().trim() === "before" ? "before" : "after";
    }
    function parseArgs(argsJson) {
      if (argsJson == null || String(argsJson).trim() === "")
        return [];
      const parsed = JSON.parse(argsJson);
      if (!Array.isArray(parsed))
        throw new Error("Heimdall JavaScript invocation args must be a JSON array.");
      return parsed;
    }
    function resolveRoot(name) {
      switch (name) {
        case "window":
          return global;
        case "globalThis":
          return global.globalThis || global;
        case "document":
          return global.document;
        default:
          return void 0;
      }
    }
    function resolveFunction(functionPath) {
      const path = String(functionPath || "").trim();
      const parts = path.split(".").filter(Boolean);
      if (parts.length < 2)
        throw new Error("Heimdall JavaScript invocation requires an explicitly rooted function path.");
      const root = resolveRoot(parts[0]);
      if (!root)
        throw new Error("Heimdall JavaScript invocation paths must start with window., globalThis., or document.");
      for (const part of parts) {
        if (!validSegment.test(part))
          throw new Error("Heimdall JavaScript invocation paths support dotted property access only.");
      }
      let owner = root;
      for (let i = 1; i < parts.length - 1; i++) {
        owner = owner == null ? void 0 : owner[parts[i]];
      }
      const functionName = parts[parts.length - 1];
      const fn = owner == null ? void 0 : owner[functionName];
      if (typeof fn !== "function")
        throw new Error(`Heimdall JavaScript function '${path}' was not found.`);
      return { owner, fn, path };
    }
    function emitError(directive, error, context) {
      const detail = {
        functionPath: directive && directive.functionPath ? directive.functionPath : null,
        timing: normalizeTiming(directive && directive.timing),
        args: directive && Array.isArray(directive.args) ? directive.args : null,
        sourceEl: directive && directive.sourceEl ? directive.sourceEl : null,
        context: context || null,
        error
      };
      emit("heimdall:javascript-error", detail);
      if (getConfig && getConfig().debug) {
        console.error("[Heimdall] JavaScript invocation failed", detail);
      }
    }
    function invokeDirective(directive, context) {
      const invocation = Object.assign({}, directive || {});
      try {
        invocation.timing = normalizeTiming(invocation.timing);
        invocation.args = Array.isArray(invocation.args) ? invocation.args : parseArgs(invocation.argsJson);
        const resolved = resolveFunction(invocation.functionPath);
        const result = resolved.fn.apply(resolved.owner, invocation.args);
        if (result && typeof result.then === "function") {
          result.then(null, (error) => {
            emitError(invocation, error, context);
          });
        }
        dbg("js invoke void", {
          functionPath: resolved.path,
          timing: invocation.timing,
          args: invocation.args
        });
        return true;
      } catch (error) {
        emitError(invocation, error, context);
        return false;
      }
    }
    function invokeAll(directives, context) {
      if (!Array.isArray(directives) || directives.length === 0)
        return 0;
      let count = 0;
      for (const directive of directives) {
        if (invokeDirective(directive, context))
          count++;
      }
      return count;
    }
    return {
      invokeAll,
      normalizeTiming
    };
  }

  // core/mutations.js
  function createMutationRuntime({ global, emitLifecycle, dbg, boot, busyState }) {
    function mutationError(sourceEl, directive, context, code, message, error) {
      const detail = {
        origin: context && context.kind ? context.kind : "action",
        sourceElement: sourceEl || null,
        requestContext: context && context.requestContext ? context.requestContext : null,
        directive,
        code,
        message,
        error: error || null
      };
      if (emitLifecycle)
        emitLifecycle(sourceEl, "heimdall:mutation-error", detail);
      dbg("mutation error", detail);
      return { applied: false, cancelled: false, targets: [], error: detail };
    }
    function validateAttributeName(name) {
      const probe = document.createElement("div");
      probe.setAttribute(name, "");
    }
    function classTokens(raw) {
      return String(raw || "").split(/\s+/).map((token) => token.trim()).filter(Boolean);
    }
    function parseOperations(directive) {
      const operations = [];
      for (const child of Array.from(directive.children || [])) {
        const tag = String(child.localName || "").toLowerCase();
        if (tag === "mutation-attr") {
          const name = (getAttr(child, "name") || "").trim();
          if (!name)
            throw new Error("mutation-attr requires a non-empty name attribute.");
          validateAttributeName(name);
          operations.push(child.hasAttribute("value") ? { type: "attribute", action: "set", name, value: child.getAttribute("value") || "" } : { type: "attribute", action: "remove", name, value: null });
          continue;
        }
        if (tag === "mutation-class") {
          const hasAdd = child.hasAttribute("add");
          const hasRemove = child.hasAttribute("remove");
          if (hasAdd === hasRemove)
            throw new Error("mutation-class requires exactly one of add or remove.");
          const action = hasAdd ? "add" : "remove";
          const tokens = classTokens(child.getAttribute(action));
          if (tokens.length === 0)
            throw new Error(`mutation-class ${action} requires at least one class token.`);
          operations.push({ type: "class", action, tokens });
          continue;
        }
        throw new Error(`Unsupported mutation operation '${tag || "unknown"}'.`);
      }
      return operations;
    }
    function resolveRoots(targetSelector, allTargets) {
      return allTargets ? Array.from(document.querySelectorAll(targetSelector)) : [document.querySelector(targetSelector)].filter(Boolean);
    }
    function resolveTargets(roots, scope, selector) {
      const targets = [];
      const seen = /* @__PURE__ */ new Set();
      function add(el) {
        if (!isElement(el) || seen.has(el))
          return;
        seen.add(el);
        targets.push(el);
      }
      for (const root of roots) {
        if (scope === "self") {
          add(root);
          continue;
        }
        if (scope === "subtree") {
          add(root);
          for (const el of root.querySelectorAll("*"))
            add(el);
          continue;
        }
        for (const el of root.querySelectorAll(selector))
          add(el);
      }
      return targets;
    }
    function applyOperation(target, operation) {
      if (operation.type === "attribute") {
        if (operation.action === "set")
          target.setAttribute(operation.name, operation.value);
        else
          target.removeAttribute(operation.name);
        if (busyState && typeof busyState.rebaseAttribute === "function") {
          busyState.rebaseAttribute(
            target,
            operation.name,
            operation.action === "set",
            operation.value
          );
        }
        return;
      }
      if (operation.action === "add")
        target.classList.add(...operation.tokens);
      else
        target.classList.remove(...operation.tokens);
    }
    function requiresBehaviorReconciliation(operation) {
      if (!operation || operation.type !== "attribute")
        return false;
      const name = String(operation.name || "").toLowerCase();
      return name === "lang" || name.startsWith("heimdall-");
    }
    function apply(directive, sourceEl, context) {
      context = context || {};
      const targetSelector = (getAttr(directive, "heimdall-content-target") || "").trim();
      if (!targetSelector)
        return mutationError(sourceEl, directive, context, "missing-target", "Mutation target selector is required.");
      const scope = (getAttr(directive, "scope") || "self").trim().toLowerCase();
      if (scope !== "self" && scope !== "subtree" && scope !== "select")
        return mutationError(sourceEl, directive, context, "invalid-scope", `Unsupported mutation scope '${scope}'.`);
      const selector = (getAttr(directive, "selector") || "").trim();
      if (scope === "select" && !selector)
        return mutationError(sourceEl, directive, context, "missing-selector", "Select-scoped mutation requires a selector.");
      let operations;
      let roots;
      let targets;
      try {
        operations = parseOperations(directive);
        roots = resolveRoots(targetSelector, directive.hasAttribute("all"));
        targets = resolveTargets(roots, scope, selector);
      } catch (error) {
        return mutationError(sourceEl, directive, context, "invalid-directive", error.message, error);
      }
      if (roots.length === 0)
        return mutationError(sourceEl, directive, context, "target-not-found", `Mutation target '${targetSelector}' was not found.`);
      const detail = {
        origin: context.kind || "action",
        sourceElement: sourceEl || null,
        requestContext: context.requestContext || null,
        directive,
        targetSelector,
        scope,
        selector: scope === "select" ? selector : null,
        allTargets: directive.hasAttribute("all"),
        rootTargets: roots,
        targets,
        operations
      };
      if (emitLifecycle && !emitLifecycle(
        sourceEl,
        "heimdall:mutation-before",
        detail,
        { cancelable: true }
      )) {
        return { applied: false, cancelled: true, targets: [], detail };
      }
      const startedAt = global.performance && typeof global.performance.now === "function" ? global.performance.now() : Date.now();
      try {
        for (const target of targets) {
          for (const operation of operations)
            applyOperation(target, operation);
        }
      } catch (error) {
        return mutationError(sourceEl, directive, context, "apply-failed", error.message, error);
      }
      const finishedAt = global.performance && typeof global.performance.now === "function" ? global.performance.now() : Date.now();
      const durationMs = Math.max(0, finishedAt - startedAt);
      if (emitLifecycle) {
        emitLifecycle(sourceEl, "heimdall:mutation-after", {
          ...detail,
          rootCount: roots.length,
          targetCount: targets.length,
          operationCount: operations.length,
          durationMs
        });
      }
      if (durationMs > 16)
        dbg("long mutation batch", { targetSelector, targetCount: targets.length, operationCount: operations.length, durationMs });
      const reconcileTargets = operations.some(requiresBehaviorReconciliation) ? targets : [];
      return { applied: true, cancelled: false, targets, reconcileTargets, detail };
    }
    function reconcile(targets) {
      const seen = /* @__PURE__ */ new Set();
      for (const target of targets || []) {
        if (!isElement(target) || !target.isConnected || seen.has(target))
          continue;
        seen.add(target);
        try {
          boot(target);
        } catch (error) {
          dbg("mutation reconciliation failed", { target, error });
        }
      }
    }
    return {
      apply,
      reconcile
    };
  }

  // core/page-lifecycle.js
  function createPageLifecycleRuntime({
    global,
    runActionFromElement,
    emit
  }) {
    function invokeMatching(attr, triggerName) {
      const elements = document.querySelectorAll(`[${attr}]`);
      for (const el of elements) {
        const actionId = (getAttr(el, attr) || "").trim();
        if (!actionId)
          continue;
        runActionFromElement(el, actionId, triggerName).catch(() => {
        });
      }
    }
    function handleVisibilityChange() {
      if (document.visibilityState !== "visible")
        return;
      invokeMatching("heimdall-content-document-visible", "document-visible");
    }
    function handleOnline() {
      invokeMatching("heimdall-content-online", "online");
    }
    function handleOffline() {
      emit("heimdall:offline", { online: false });
    }
    function install() {
      if (document.__heimdallPageLifecycleInstalled)
        return;
      document.__heimdallPageLifecycleInstalled = true;
      document.addEventListener("visibilitychange", handleVisibilityChange, false);
      global.addEventListener("online", handleOnline, false);
      global.addEventListener("offline", handleOffline, false);
    }
    return { install };
  }

  // core/payloads.js
  function createPayloadResolver(global) {
    function findClosestStateElement(el, key) {
      let cur = el;
      while (cur && cur.nodeType === 1) {
        if (key) {
          const attr = `data-heimdall-state-${key}`;
          if (cur.hasAttribute && cur.hasAttribute(attr))
            return cur;
        } else {
          if (cur.hasAttribute && cur.hasAttribute("data-heimdall-state"))
            return cur;
        }
        cur = cur.parentElement;
      }
      return null;
    }
    function readStateAttribute(host, attr) {
      if (!host || !host.hasAttribute || !host.hasAttribute(attr))
        return null;
      const raw = host.getAttribute(attr);
      if (!raw)
        return null;
      return safeJsonParse(raw);
    }
    function createClosestStateBinding(el, key) {
      const host = findClosestStateElement(el, key);
      if (!host)
        return null;
      const attribute = key ? `data-heimdall-state-${key}` : "data-heimdall-state";
      return {
        kind: "closest-state",
        host,
        attribute,
        value: readStateAttribute(host, attribute),
        resolve() {
          if (host.isConnected === false || !host.hasAttribute(attribute)) {
            return {
              available: false,
              value: null
            };
          }
          return {
            available: true,
            value: readStateAttribute(host, attribute)
          };
        }
      };
    }
    function resolvePayloadRef(el) {
      const ref = getAttr(el, "heimdall-payload-ref");
      if (ref)
        return getByPath(global, ref);
      const from = (getAttr(el, "heimdall-payload-from") || "").trim();
      if (from.toLowerCase().startsWith("ref:")) {
        const path = from.substring(4).trim();
        return getByPath(global, path);
      }
      return void 0;
    }
    function payloadBindingFromElement(el) {
      const payloadAttr = getAttr(el, "heimdall-payload");
      if (payloadAttr)
        return { value: safeJsonParse(payloadAttr), binding: null };
      const refObj = resolvePayloadRef(el);
      if (refObj !== void 0)
        return { value: refObj, binding: null };
      const fromRaw = (getAttr(el, "heimdall-payload-from") || "").trim();
      const from = fromRaw.toLowerCase();
      if (from === "closest-state" || from.startsWith("closest-state:")) {
        const key = from.startsWith("closest-state:") ? fromRaw.substring("closest-state:".length).trim() : null;
        const binding = createClosestStateBinding(el, key || null);
        return binding ? { value: binding.value, binding } : { value: null, binding: null };
      }
      if (!from)
        return { value: null, binding: null };
      if (from === "closest-form") {
        const form2 = el.closest("form");
        if (!form2)
          return { value: null, binding: null };
        return { value: formDataToPayload(new FormData(form2)), binding: null };
      }
      if (from === "self") {
        const obj = {};
        for (const key in el.dataset) obj[key] = el.dataset[key];
        return { value: obj, binding: null };
      }
      const form = document.querySelector(fromRaw);
      if (form && form.tagName === "FORM") {
        return { value: formDataToPayload(new FormData(form)), binding: null };
      }
      return { value: null, binding: null };
    }
    function payloadFromElement(el) {
      return payloadBindingFromElement(el).value;
    }
    return {
      payloadBindingFromElement,
      payloadFromElement
    };
  }

  // core/request-coordinator.js
  var VALID_SYNC_STRATEGIES = /* @__PURE__ */ new Set([
    "parallel",
    "replace",
    "drop",
    "queue-latest"
  ]);
  function createRequestCoordinator({ global, dbg }) {
    const elementStates = /* @__PURE__ */ new WeakMap();
    const groupStates = /* @__PURE__ */ new Map();
    const recordsByContext = /* @__PURE__ */ new WeakMap();
    function normalizeStrategy(value, fallback) {
      const normalized = String(value || "").trim().toLowerCase();
      if (VALID_SYNC_STRATEGIES.has(normalized))
        return normalized;
      const normalizedFallback = String(fallback || "parallel").trim().toLowerCase();
      if (VALID_SYNC_STRATEGIES.has(normalizedFallback))
        return normalizedFallback;
      return "parallel";
    }
    function cancellationResult(context, reason) {
      return {
        ok: false,
        status: 0,
        error: null,
        response: null,
        html: null,
        ms: Math.max(0, performance.now() - context.startedAt),
        cancelled: true,
        cancelReason: reason || "cancelled",
        requestId: context.requestId
      };
    }
    function getState(record) {
      if (record.group) {
        let state = groupStates.get(record.group);
        if (!state) {
          state = { active: null, queued: null, generation: 0, group: record.group };
          groupStates.set(record.group, state);
        }
        return state;
      }
      if (record.context.sourceElement) {
        let state = elementStates.get(record.context.sourceElement);
        if (!state) {
          state = { active: null, queued: null, generation: 0, group: null };
          elementStates.set(record.context.sourceElement, state);
        }
        return state;
      }
      return null;
    }
    function cleanupState(state) {
      if (!state || state.active || state.queued)
        return;
      if (state.group)
        groupStates.delete(state.group);
    }
    function abortController(record) {
      if (!record.controller)
        return;
      try {
        record.controller.abort(record.cancelReason);
      } catch {
        try {
          record.controller.abort();
        } catch {
        }
      }
    }
    function settleQueuedCancellation(record, reason) {
      if (!record || record.settled)
        return;
      record.cancelled = true;
      record.cancelReason = reason || "cancelled";
      record.context.cancelled = true;
      record.context.cancelReason = record.cancelReason;
      cleanupRecord(record);
      record.settled = true;
      record.resolve(cancellationResult(record.context, record.cancelReason));
    }
    function cancelRecord(record, reason) {
      if (!record || record.settled || record.cancelled)
        return false;
      record.cancelled = true;
      record.cancelReason = reason || "cancelled";
      record.context.cancelled = true;
      record.context.cancelReason = record.cancelReason;
      if (record.started) {
        abortController(record);
      } else {
        if (record.state && record.state.queued === record)
          record.state.queued = null;
        settleQueuedCancellation(record, record.cancelReason);
        cleanupState(record.state);
      }
      return true;
    }
    function linkExternalSignal(record) {
      const signal = record.context.externalSignal;
      if (!signal || typeof signal.addEventListener !== "function")
        return;
      if (signal.aborted) {
        cancelRecord(record, "external-signal");
        return;
      }
      const handler = () => cancelRecord(record, "external-signal");
      signal.addEventListener("abort", handler, { once: true });
      record.removeExternalAbort = () => {
        try {
          signal.removeEventListener("abort", handler);
        } catch {
        }
      };
    }
    function startTimeout(record) {
      const timeoutMs = Number(record.context.timeoutMs || 0);
      if (!Number.isFinite(timeoutMs) || timeoutMs <= 0)
        return;
      record.timeoutId = global.setTimeout(() => {
        record.timedOut = true;
        record.context.timedOut = true;
        cancelRecord(record, "timeout");
      }, timeoutMs);
    }
    function cleanupRecord(record) {
      if (record.timeoutId != null) {
        global.clearTimeout(record.timeoutId);
        record.timeoutId = null;
      }
      if (record.removeExternalAbort) {
        record.removeExternalAbort();
        record.removeExternalAbort = null;
      }
    }
    async function startRecord(record) {
      if (record.settled)
        return;
      const state = record.state;
      if (state) {
        state.active = record;
        record.generation = ++state.generation;
      }
      record.started = true;
      record.context.startedExecutionAt = performance.now();
      startTimeout(record);
      let result;
      try {
        if (record.cancelled) {
          result = cancellationResult(record.context, record.cancelReason);
        } else {
          result = await record.execute();
        }
      } catch (error) {
        cleanupRecord(record);
        record.settled = true;
        record.reject(error);
        if (state && state.active === record) {
          state.active = null;
          const queued = state.queued;
          state.queued = null;
          if (queued)
            startRecord(queued);
          else
            cleanupState(state);
        }
        return;
      }
      cleanupRecord(record);
      record.settled = true;
      record.resolve(result);
      if (state && state.active === record) {
        state.active = null;
        const queued = state.queued;
        state.queued = null;
        if (queued)
          startRecord(queued);
        else
          cleanupState(state);
      }
    }
    function run(context, execute) {
      return new Promise((resolve, reject) => {
        const Controller = global.AbortController || globalThis.AbortController;
        const controller = typeof Controller === "function" ? new Controller() : null;
        const record = {
          context,
          execute,
          resolve,
          reject,
          controller,
          state: null,
          group: context.syncGroup ? String(context.syncGroup).trim() : null,
          generation: 0,
          started: false,
          settled: false,
          cancelled: false,
          cancelReason: null,
          timedOut: false,
          timeoutId: null,
          removeExternalAbort: null
        };
        context.controller = controller;
        context.signal = controller ? controller.signal : null;
        recordsByContext.set(context, record);
        linkExternalSignal(record);
        if (record.cancelled) {
          settleQueuedCancellation(record, record.cancelReason);
          return;
        }
        const strategy = normalizeStrategy(context.sync, "parallel");
        context.sync = strategy;
        if (strategy === "parallel") {
          startRecord(record);
          return;
        }
        const state = getState(record);
        record.state = state;
        if (!state || !state.active) {
          startRecord(record);
          return;
        }
        switch (strategy) {
          case "replace":
            cancelRecord(state.active, "replaced");
            if (state.queued)
              cancelRecord(state.queued, "queue-replaced");
            state.queued = null;
            startRecord(record);
            break;
          case "drop":
            settleQueuedCancellation(record, "dropped");
            break;
          case "queue-latest":
            if (state.queued)
              cancelRecord(state.queued, "queue-replaced");
            state.queued = record;
            break;
          default:
            dbg("unknown request synchronization strategy; using parallel", { strategy });
            startRecord(record);
            break;
        }
      });
    }
    function cancel(context, reason) {
      return cancelRecord(recordsByContext.get(context), reason);
    }
    function isCurrent(context) {
      const record = recordsByContext.get(context);
      if (!record || record.cancelled)
        return false;
      if (!record.state)
        return true;
      return record.state.active === record && record.state.generation === record.generation;
    }
    function getCancellationResult(context) {
      const record = recordsByContext.get(context);
      const reason = record && record.cancelReason ? record.cancelReason : context.cancelReason || "cancelled";
      return cancellationResult(context, reason);
    }
    return {
      cancel,
      getCancellationResult,
      isCurrent,
      normalizeStrategy,
      run
    };
  }

  // core/security-tokens.js
  function createSecurityTokens({
    global,
    getConfig,
    emit,
    emitLifecycle,
    dbg,
    safeText: safeText2,
    resolveRequestHeaders,
    csrfHeader,
    defaultBifrostTokenEndpoint
  }) {
    let csrfToken = null;
    let csrfTokenPromise = null;
    const _bifrostTokenByTopic = /* @__PURE__ */ new Map();
    const _bifrostTokenPromiseByTopic = /* @__PURE__ */ new Map();
    async function ensureCsrfToken() {
      if (csrfToken)
        return csrfToken;
      if (csrfTokenPromise)
        return csrfTokenPromise;
      csrfTokenPromise = (async () => {
        try {
          const configuredUrl = getConfig().endpoints.csrf;
          const url = new URL(configuredUrl, global.location?.origin || void 0).toString();
          let headers = { "X-Requested-With": "XMLHttpRequest" };
          if (typeof resolveRequestHeaders === "function") {
            headers = await resolveRequestHeaders({
              kind: "csrf-token",
              url,
              method: "GET",
              actionId: null,
              topic: null,
              requestId: null,
              attempt: 1,
              sourceElement: null,
              signal: null
            }, headers);
          }
          const res = await global.fetch(url, {
            method: "GET",
            credentials: "same-origin",
            headers
          });
          if (!res.ok) {
            const body = await safeText2(res);
            const authRedirectUrl = getAuthRedirectUrlFromResponse(res);
            const redirectUrl = authRedirectUrl ? normalizeFollowedAuthRedirectUrl(global, getConfig, authRedirectUrl) : null;
            const useDefaultUnauthorizedHandling = emitUnauthorized({
              response: res,
              emitLifecycle,
              sourceElement: null,
              detail: {
                kind: "csrf-token",
                actionId: null,
                topic: null,
                requestId: null,
                attempt: 1,
                sourceElement: null,
                url,
                method: "GET",
                body,
                redirectUrl,
                requestContext: null
              }
            });
            const performedRedirect = !!(authRedirectUrl && useDefaultUnauthorizedHandling);
            if (performedRedirect) {
              if (typeof dbg === "function")
                dbg("csrf token redirecting", { redirectUrl });
              global.location.href = redirectUrl;
            }
            const error = new Error(`CSRF token fetch failed: ${res.status}. ${body || ""}`.trim());
            error.status = res.status;
            error.body = body;
            error.redirectUrl = performedRedirect ? redirectUrl : null;
            throw error;
          }
          const data = await res.json();
          csrfToken = data && data.requestToken;
          if (!csrfToken)
            throw new Error("CSRF response missing requestToken.");
          return csrfToken;
        } finally {
          csrfTokenPromise = null;
        }
      })();
      return csrfTokenPromise;
    }
    function clearCsrfToken() {
      csrfToken = null;
      csrfTokenPromise = null;
      _bifrostTokenByTopic.clear();
      _bifrostTokenPromiseByTopic.clear();
    }
    function clearBifrostSubscribeToken(topic) {
      const t = String(topic || "").trim();
      if (!t) {
        _bifrostTokenByTopic.clear();
        _bifrostTokenPromiseByTopic.clear();
        return;
      }
      _bifrostTokenByTopic.delete(t);
      _bifrostTokenPromiseByTopic.delete(t);
    }
    function isAntiforgeryFailure(status, body) {
      if (Number(status) !== 400)
        return false;
      const lower = String(body || "").toLowerCase();
      return lower.includes("csrf") || lower.includes("antiforgery");
    }
    async function fetchBifrostSubscribeToken(t, shouldRetry) {
      const config = getConfig();
      const antiforgeryEnabled = config.antiforgery !== false;
      const csrf = antiforgeryEnabled ? await ensureCsrfToken() : null;
      const base = config.endpoints && config.endpoints.bifrostToken ? config.endpoints.bifrostToken : defaultBifrostTokenEndpoint;
      const url = new URL(base, global.location?.origin || void 0);
      url.searchParams.set("topic", t);
      let headers = {
        "X-Requested-With": "XMLHttpRequest"
      };
      if (antiforgeryEnabled)
        headers[csrfHeader] = csrf;
      if (typeof resolveRequestHeaders === "function") {
        headers = await resolveRequestHeaders({
          kind: "bifrost-token",
          url: url.toString(),
          method: "GET",
          actionId: null,
          topic: t,
          requestId: null,
          attempt: shouldRetry ? 1 : 2,
          sourceElement: null,
          signal: null
        }, headers);
      }
      const res = await global.fetch(url.toString(), {
        method: "GET",
        credentials: "same-origin",
        headers
      });
      let responseBody = null;
      if (res.status === 401)
        responseBody = await safeText2(res);
      const authRedirectUrl = getAuthRedirectUrlFromResponse(res);
      const normalizedAuthRedirectUrl = authRedirectUrl ? normalizeFollowedAuthRedirectUrl(global, getConfig, authRedirectUrl) : null;
      const useDefaultUnauthorizedHandling = emitUnauthorized({
        response: res,
        emitLifecycle,
        sourceElement: null,
        detail: {
          kind: "bifrost-token",
          actionId: null,
          topic: t,
          requestId: null,
          attempt: shouldRetry ? 1 : 2,
          sourceElement: null,
          url: url.toString(),
          method: "GET",
          body: responseBody,
          redirectUrl: normalizedAuthRedirectUrl,
          requestContext: null
        }
      });
      if (authRedirectUrl && useDefaultUnauthorizedHandling) {
        const redirectUrl = normalizedAuthRedirectUrl;
        const error = new Error(`Bifrost token fetch redirected: ${redirectUrl}`);
        error.status = res.status;
        error.redirectUrl = redirectUrl;
        if (typeof emit === "function") {
          emit("heimdall:sse-redirect", {
            topic: t,
            url: url.toString(),
            status: res.status,
            redirectUrl
          });
        }
        if (typeof dbg === "function")
          dbg("sse token redirecting", { topic: t, redirectUrl });
        global.location.href = redirectUrl;
        throw error;
      }
      if (!res.ok) {
        const body = responseBody == null ? await safeText2(res) : responseBody;
        if (antiforgeryEnabled && shouldRetry && isAntiforgeryFailure(res.status, body)) {
          if (typeof dbg === "function")
            dbg("bifrost csrf validation suspected; retrying once with fresh token", { topic: t });
          clearCsrfToken();
          return fetchBifrostSubscribeToken(t, false);
        }
        const error = new Error(`Bifrost token fetch failed: ${res.status}. ${body || ""}`.trim());
        error.status = res.status;
        error.body = body;
        throw error;
      }
      const data = await res.json();
      const token = data && (data.token || data.st);
      const expiresInSeconds = data && (data.expiresInSeconds || data.expires_in_seconds || 120);
      if (!token)
        throw new Error("Bifrost token response missing token.");
      const ttlMs = Math.max(5, parseInt(expiresInSeconds, 10) || 120) * 1e3;
      const expiresAtMs = Date.now() + Math.max(5e3, ttlMs - 5e3);
      _bifrostTokenByTopic.set(t, { token, expiresAtMs });
      return token;
    }
    async function ensureBifrostSubscribeToken(topic) {
      const t = String(topic || "").trim();
      if (!t)
        throw new Error("Bifrost topic is required.");
      const cached = _bifrostTokenByTopic.get(t);
      if (cached && cached.token && cached.expiresAtMs && Date.now() < cached.expiresAtMs) {
        return cached.token;
      }
      const inflight = _bifrostTokenPromiseByTopic.get(t);
      if (inflight)
        return inflight;
      const p = (async () => {
        try {
          return await fetchBifrostSubscribeToken(t, true);
        } finally {
          _bifrostTokenPromiseByTopic.delete(t);
        }
      })();
      _bifrostTokenPromiseByTopic.set(t, p);
      return p;
    }
    return {
      clearBifrostSubscribeToken,
      clearCsrfToken,
      ensureBifrostSubscribeToken,
      ensureCsrfToken
    };
  }

  // core/startup.js
  function createHeimdallRuntime({
    global,
    apiVersion,
    defaultBasePath,
    defaultContentEndpoint,
    defaultCsrfEndpoint,
    defaultBifrostTokenEndpoint,
    defaultBifrostEndpoint,
    invoke,
    boot,
    onReady: onReady2,
    clearCsrfToken,
    sseConnect,
    sseDisconnect,
    sseDisconnectAll,
    handlers,
    installPageLifecycle,
    installSseSweeper,
    dbg,
    onRuntimeCreated
  }) {
    function installObserver() {
      if (!Heimdall.config.observeDom)
        return;
      if (document.__heimdallObserverInstalled)
        return;
      document.__heimdallObserverInstalled = true;
      let pending = /* @__PURE__ */ new Set();
      let scheduled = false;
      function flush() {
        scheduled = false;
        const nodes = Array.from(pending);
        pending.clear();
        for (const node of nodes)
          boot(node);
      }
      function scheduleFlush() {
        if (scheduled)
          return;
        scheduled = true;
        Promise.resolve().then(flush);
      }
      const attributeFilter = [
        "heimdall-content-load",
        "heimdall-content-visible",
        "heimdall-content-scroll",
        "heimdall-poll",
        "heimdall-visible-once",
        "heimdall-sse",
        "heimdall-sse-topic",
        "heimdall-sse-target",
        "heimdall-sse-swap",
        "heimdall-sse-event",
        "heimdall-sse-disable",
        "heimdall-time",
        "heimdall-time-format",
        "lang"
      ];
      const obs = new MutationObserver((mutations) => {
        for (const m of mutations) {
          if (m.type === "attributes") {
            if (m.target && m.target.nodeType === 1)
              pending.add(m.target);
            continue;
          }
          for (const n of m.addedNodes) {
            if (!n || n.nodeType !== 1)
              continue;
            pending.add(n);
          }
        }
        if (pending.size) scheduleFlush();
      });
      obs.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter });
      if (document.documentElement && document.documentElement !== document.body) {
        obs.observe(document.documentElement, {
          attributes: true,
          attributeFilter: ["lang"]
        });
      }
      Heimdall._observer = obs;
      dbg("MutationObserver installed");
    }
    const Heimdall = {
      apiVersion,
      invoke,
      boot,
      onReady: onReady2,
      clearCsrfToken,
      sse: {
        connect: sseConnect,
        disconnect: sseDisconnect,
        disconnectAll: sseDisconnectAll
      },
      _observer: null,
      config: {
        basePath: defaultBasePath,
        apiVersion,
        endpoints: {
          contentActions: defaultContentEndpoint,
          csrf: defaultCsrfEndpoint,
          bifrostToken: defaultBifrostTokenEndpoint,
          bifrost: defaultBifrostEndpoint
        },
        observeDom: true,
        debug: false,
        authReturnUrlParameter: "ReturnUrl",
        requestHeaders: null,
        antiforgery: true,
        clientInfo: false,
        clientInfoMaxAgeMs: 6e4,
        inputDebounceMs: 250,
        hoverDelayMs: 150,
        scrollThresholdPx: 120,
        scrollMinIntervalMs: 250,
        requestSync: "parallel",
        requestTimeoutMs: 0,
        // NOTE: visibleRootMargin and visibleThreshold are read once when
        // the IntersectionObserver is first created. Set these values before
        // any heimdall-content-visible element is booted (i.e. before DOMContentLoaded).
        visibleRootMargin: "0px",
        visibleThreshold: 0,
        oobEnabled: true,
        sseDefaultSwap: "none",
        sseEventName: "heimdall",
        sseReconnectDelayMs: 250,
        sseReconnectMaxDelayMs: 1e4,
        sseReconnectBackoffFactor: 2,
        sseSweepIntervalMs: 5e3,
        ssePauseWhenHidden: false
      }
    };
    if (typeof onRuntimeCreated === "function")
      onRuntimeCreated(Heimdall);
    global.Heimdall = Heimdall;
    onReady2(() => {
      if (!document.__heimdallDelegatesInstalled) {
        document.__heimdallDelegatesInstalled = true;
        document.addEventListener("click", handlers.handleClick, true);
        document.addEventListener("change", handlers.handleChange, false);
        document.addEventListener("input", handlers.handleInput, false);
        document.addEventListener("submit", handlers.handleSubmit, false);
        document.addEventListener("keydown", handlers.handleKeydown, false);
        document.addEventListener("focusout", handlers.handleFocusOut, false);
        document.addEventListener("mouseover", handlers.handleMouseOver, false);
        document.addEventListener("mouseout", handlers.handleMouseOut, false);
      }
      boot(document);
      installObserver();
      installPageLifecycle();
      installSseSweeper();
      if (global.Blazor && typeof global.Blazor.addEventListener === "function") {
        global.Blazor.addEventListener("enhancedload", () => {
          boot(document);
        });
      }
    });
    return Heimdall;
  }

  // core/sse.js
  function createSseRuntime({
    global,
    getConfig,
    emit,
    dbg,
    dom,
    boot,
    clearBifrostSubscribeToken,
    ensureBifrostSubscribeToken,
    matchesTriggerAttr,
    defaultBifrostEndpoint
  }) {
    const _sseByElement = /* @__PURE__ */ new WeakMap();
    const _sseStates = /* @__PURE__ */ new Set();
    const _sseConnections = /* @__PURE__ */ new Map();
    function getSseTopic(el) {
      const t1 = getAttr(el, "heimdall-sse");
      if (t1 && t1.trim())
        return t1.trim();
      const t2 = getAttr(el, "heimdall-sse-topic");
      if (t2 && t2.trim())
        return t2.trim();
      return null;
    }
    function readSseConfig(el, options) {
      options = options || {};
      const config = getConfig();
      const topic = options.topic != null ? String(options.topic || "").trim() : getSseTopic(el);
      const eventName = (options.event != null ? String(options.event || "") : getAttr(el, "heimdall-sse-event") || config.sseEventName || "heimdall").trim();
      const target = options.target != null ? options.target : getAttr(el, "heimdall-sse-target") || el;
      const swap = String(options.swap != null ? options.swap : getAttr(el, "heimdall-sse-swap") || config.sseDefaultSwap || "none").toLowerCase();
      const disabled = options.disable != null ? !!options.disable : truthyAttr(el, "heimdall-sse-disable", false);
      return {
        topic,
        eventName,
        target,
        swap,
        disabled,
        programmatic: !!options.programmatic
      };
    }
    function getBifrostBaseUrl() {
      const config = getConfig();
      return config.endpoints && config.endpoints.bifrost ? config.endpoints.bifrost : defaultBifrostEndpoint;
    }
    function buildBifrostUrl(topic, st) {
      const url = new URL(getBifrostBaseUrl(), global.location?.origin || void 0);
      url.searchParams.set("topic", topic);
      if (st)
        url.searchParams.set("st", st);
      return url.toString();
    }
    function getConnectionKey(topic) {
      const url = new URL(getBifrostBaseUrl(), global.location?.origin || void 0);
      return `${url.toString()}
${topic}`;
    }
    function getStateUrl(state) {
      if (!state)
        return null;
      if (state.connection && !state.closed)
        return state.connection.url || state.url || null;
      return state.url || null;
    }
    function clearReconnectTimer(connection) {
      if (!connection || !connection.reconnectTimerId)
        return;
      try {
        global.clearTimeout(connection.reconnectTimerId);
      } catch {
      }
      connection.reconnectTimerId = null;
    }
    function closeEventSource(connection) {
      if (!connection)
        return;
      const es = connection.es;
      connection.es = null;
      connection.connecting = false;
      connection.eventHandlers.clear();
      try {
        if (es)
          es.close();
      } catch {
      }
    }
    function closeSseConnection(connection, reason) {
      if (!connection || connection.closed)
        return;
      connection.closed = true;
      connection.paused = false;
      connection.pauseReason = null;
      connection.connectAttempt++;
      clearReconnectTimer(connection);
      closeEventSource(connection);
      try {
        _sseConnections.delete(connection.key);
      } catch {
      }
      dbg("sse connection closed", { topic: connection.topic, reason: reason || "closed" });
    }
    function closeSseState(state, reason) {
      if (!state || state.closed)
        return;
      const connection = state.connection;
      state.url = getStateUrl(state);
      state.closed = true;
      state.paused = false;
      state.pauseReason = null;
      try {
        _sseByElement.delete(state.el);
      } catch {
      }
      try {
        _sseStates.delete(state);
      } catch {
      }
      if (connection) {
        try {
          connection.subscribers.delete(state);
          pruneConnectionEventListeners(connection);
        } catch {
        }
      }
      state.connection = null;
      emit("heimdall:sse-close", {
        topic: state.topic,
        url: state.url,
        reason: reason || "closed",
        el: state.el
      });
      dbg("sse closed", { topic: state.topic, reason: reason || "closed" });
      if (connection && !connection.closed && connection.subscribers.size === 0)
        closeSseConnection(connection, reason || "empty");
    }
    function closeSseConnectionSubscribers(connection, reason) {
      if (!connection)
        return;
      for (const state of Array.from(connection.subscribers)) {
        closeSseState(state, reason);
      }
      closeSseConnection(connection, reason);
    }
    function getReconnectDelayMs(connection) {
      const config = getConfig();
      const initial = Math.max(0, numberConfig(config.sseReconnectDelayMs, 250));
      const max = Math.max(initial, numberConfig(config.sseReconnectMaxDelayMs, 1e4));
      const factor = Math.max(1, numberConfig(config.sseReconnectBackoffFactor, 2));
      const delay = initial * Math.pow(factor, Math.max(0, connection.retryCount));
      return Math.min(max, delay);
    }
    function numberConfig(value, fallback) {
      const n = Number(value);
      return Number.isFinite(n) ? n : fallback;
    }
    function isPermanentTokenFailure(error) {
      const status = error && Number(error.status);
      return status === 400 || status === 403 || status === 404;
    }
    function tokenFailureReason(error) {
      const status = error && Number(error.status);
      if (status === 400)
        return "token-rejected";
      if (status === 403)
        return "token-forbidden";
      if (status === 404)
        return "token-endpoint-not-found";
      return "token-failed";
    }
    function validateStateElement(state) {
      if (!state || state.closed)
        return false;
      if (!state.el || !state.el.isConnected) {
        closeSseState(state, "disconnected");
        return false;
      }
      if (truthyAttr(state.el, "heimdall-sse-disable", false)) {
        closeSseState(state, "disabled");
        return false;
      }
      if (!state.programmatic) {
        const currentTopic = getSseTopic(state.el);
        if (!currentTopic) {
          closeSseState(state, "topic-removed");
          return false;
        }
        if (currentTopic !== state.topic) {
          const el = state.el;
          closeSseState(state, "topic-changed");
          attachSse(el);
          return false;
        }
      }
      return true;
    }
    function validateSseConnection(connection) {
      if (!connection || connection.closed)
        return false;
      for (const state of Array.from(connection.subscribers)) {
        validateStateElement(state);
      }
      if (connection.closed)
        return false;
      if (connection.subscribers.size === 0) {
        closeSseConnection(connection, "empty");
        return false;
      }
      return true;
    }
    function isOffline() {
      return !!(global.navigator && global.navigator.onLine === false);
    }
    function getPauseReason() {
      if (isOffline())
        return "offline";
      if (getConfig().ssePauseWhenHidden && document.hidden)
        return "hidden";
      return null;
    }
    function emitForConnectionSubscribers(connection, eventName, detail) {
      if (!connection)
        return;
      for (const state of Array.from(connection.subscribers)) {
        if (state.closed)
          continue;
        emit(eventName, {
          ...detail,
          topic: connection.topic,
          url: connection.url,
          el: state.el
        });
      }
    }
    function pauseSseConnection(connection, reason, options) {
      options = options || {};
      if (!connection || connection.closed)
        return false;
      if (!validateSseConnection(connection))
        return false;
      const nextReason = reason || "paused";
      const wasPaused = !!connection.paused;
      const previousReason = connection.pauseReason || null;
      connection.paused = true;
      connection.pauseReason = nextReason;
      for (const state of Array.from(connection.subscribers)) {
        state.paused = true;
        state.pauseReason = nextReason;
      }
      if (!options.prepared) {
        connection.connectAttempt++;
        clearReconnectTimer(connection);
        closeEventSource(connection);
      } else {
        clearReconnectTimer(connection);
      }
      if (!wasPaused || previousReason !== nextReason) {
        emitForConnectionSubscribers(connection, "heimdall:sse-pause", {
          reason: nextReason,
          previousReason
        });
        dbg("sse paused", { topic: connection.topic, reason: nextReason });
      }
      return true;
    }
    function pauseAllSse(reason) {
      for (const connection of Array.from(_sseConnections.values())) {
        pauseSseConnection(connection, reason);
      }
    }
    function resumeSseConnection(connection, reason) {
      if (!connection || connection.closed || !connection.paused)
        return false;
      if (!validateSseConnection(connection))
        return false;
      const blockedReason = getPauseReason();
      if (blockedReason) {
        pauseSseConnection(connection, blockedReason);
        return false;
      }
      const previousReason = connection.pauseReason || null;
      connection.paused = false;
      connection.pauseReason = null;
      for (const state of Array.from(connection.subscribers)) {
        state.paused = false;
        state.pauseReason = null;
      }
      emitForConnectionSubscribers(connection, "heimdall:sse-resume", {
        reason: reason || "resume",
        previousReason
      });
      dbg("sse resumed", { topic: connection.topic, reason: reason || "resume", previousReason });
      connectSseConnection(connection);
      return true;
    }
    function resumePausedSse(reason) {
      for (const connection of Array.from(_sseConnections.values())) {
        resumeSseConnection(connection, reason);
      }
    }
    function scheduleReconnect(connection, reason, error) {
      if (!connection || connection.closed)
        return;
      if (connection.paused)
        return;
      if (!validateSseConnection(connection))
        return;
      if (connection.reconnectTimerId)
        return;
      connection.connectAttempt++;
      closeEventSource(connection);
      if (typeof clearBifrostSubscribeToken === "function")
        clearBifrostSubscribeToken(connection.topic);
      const pauseReason = getPauseReason();
      if (pauseReason) {
        pauseSseConnection(connection, pauseReason, { prepared: true });
        return;
      }
      const delayMs = getReconnectDelayMs(connection);
      connection.retryCount++;
      emitForConnectionSubscribers(connection, "heimdall:sse-reconnect-scheduled", {
        reason: reason || "reconnect",
        attempt: connection.retryCount,
        delayMs,
        status: error && error.status ? error.status : null
      });
      dbg("sse reconnect scheduled", {
        topic: connection.topic,
        reason: reason || "reconnect",
        attempt: connection.retryCount,
        delayMs
      });
      connection.reconnectTimerId = global.setTimeout(() => {
        connection.reconnectTimerId = null;
        connectSseConnection(connection);
      }, delayMs);
    }
    async function connectSseConnection(connection) {
      if (!validateSseConnection(connection))
        return;
      const pauseReason = getPauseReason();
      if (pauseReason) {
        pauseSseConnection(connection, pauseReason);
        return;
      }
      clearReconnectTimer(connection);
      if (connection.es || connection.connecting)
        return;
      const attemptId = ++connection.connectAttempt;
      connection.connecting = true;
      try {
        const st = await ensureBifrostSubscribeToken(connection.topic);
        if (connection.closed || attemptId !== connection.connectAttempt)
          return;
        if (!validateSseConnection(connection))
          return;
        const url = buildBifrostUrl(connection.topic, st);
        connection.url = url;
        for (const state of Array.from(connection.subscribers)) {
          state.url = url;
        }
        let es;
        try {
          es = new global.EventSource(url);
        } catch (e) {
          connection.connecting = false;
          emitForConnectionSubscribers(connection, "heimdall:sse-error", { error: e });
          if (getConfig().debug) {
            console.error(`[Heimdall] SSE connect failed`, e);
          }
          scheduleReconnect(connection, "connect-failed", e);
          return;
        }
        if (connection.closed || attemptId !== connection.connectAttempt) {
          try {
            es.close();
          } catch {
          }
          return;
        }
        connection.es = es;
        connection.connecting = false;
        connection.eventHandlers.clear();
        es.onopen = () => {
          if (connection.closed)
            return;
          connection.retryCount = 0;
          connection.openedAt = Date.now();
          connection.lastMessageAt = Date.now();
          emitForConnectionSubscribers(connection, "heimdall:sse-open", {});
          dbg("sse open", { topic: connection.topic, url });
        };
        es.onmessage = (ev) => {
          dispatchSsePayload(connection, "message", ev, ev && ev.data != null ? ev.data : "");
        };
        syncConnectionEventListeners(connection);
        es.onerror = (e) => {
          if (connection.closed)
            return;
          emitForConnectionSubscribers(connection, "heimdall:sse-error", { error: e });
          if (getConfig().debug) {
            console.warn(`[Heimdall] SSE error; reconnecting with a fresh token`, { topic: connection.topic, url: connection.url }, e);
          }
          scheduleReconnect(connection, "eventsource-error", e);
        };
      } catch (e) {
        if (connection.closed || attemptId !== connection.connectAttempt)
          return;
        connection.connecting = false;
        if (e && e.redirectUrl)
          return;
        emitForConnectionSubscribers(connection, "heimdall:sse-error", { error: e });
        if (getConfig().debug) {
          console.error(`[Heimdall] SSE token/connect failed`, e);
        }
        if (isPermanentTokenFailure(e)) {
          closeSseConnectionSubscribers(connection, tokenFailureReason(e));
          return;
        }
        scheduleReconnect(connection, "token-failed", e);
      }
    }
    function ensureConnectionEventListener(connection, eventName) {
      if (!connection || !connection.es || !eventName || eventName === "message")
        return;
      if (connection.eventHandlers.has(eventName))
        return;
      const handler = (ev) => {
        dispatchSsePayload(connection, eventName, ev, ev && ev.data != null ? ev.data : "");
      };
      connection.eventHandlers.set(eventName, handler);
      connection.es.addEventListener(eventName, handler);
    }
    function syncConnectionEventListeners(connection) {
      if (!connection || !connection.es)
        return;
      for (const state of Array.from(connection.subscribers)) {
        if (!state.closed)
          ensureConnectionEventListener(connection, state.eventName);
      }
      pruneConnectionEventListeners(connection);
    }
    function pruneConnectionEventListeners(connection) {
      if (!connection || !connection.es || !connection.eventHandlers)
        return;
      for (const [eventName, handler] of Array.from(connection.eventHandlers.entries())) {
        const stillUsed = Array.from(connection.subscribers).some((state) => !state.closed && state.eventName === eventName);
        if (stillUsed)
          continue;
        try {
          if (typeof connection.es.removeEventListener === "function")
            connection.es.removeEventListener(eventName, handler);
        } catch {
        }
        connection.eventHandlers.delete(eventName);
      }
    }
    function dispatchSsePayload(connection, eventName, ev, rawData) {
      if (!connection || connection.closed || connection.paused)
        return;
      if (!validateSseConnection(connection))
        return;
      connection.lastMessageAt = Date.now();
      for (const state of Array.from(connection.subscribers)) {
        if (state.closed || state.paused || state.eventName !== eventName)
          continue;
        handleSsePayload(state, ev, rawData);
      }
    }
    function handleSsePayload(state, ev, rawData) {
      if (state.closed || state.paused || state.connection && state.connection.paused)
        return;
      if (!state.el || !state.el.isConnected) {
        closeSseState(state, "disconnected");
        return;
      }
      const data = rawData != null ? String(rawData) : "";
      const targetEl = resolveTarget(state.target, state.el);
      const swapMode = state.swap || "none";
      const url = getStateUrl(state);
      let html = data;
      let abortSwap = false;
      let abortReason = null;
      let redirectUrl = null;
      let jsAfter = [];
      let mutationTargets = [];
      try {
        const oob = dom.processOob(html, state.el, {
          phase: "before",
          kind: "sse",
          topic: state.topic,
          event: state.eventName,
          url,
          target: targetEl,
          swap: swapMode
        });
        html = oob.html;
        abortSwap = !!oob.abortSwap;
        abortReason = oob.abortReason || null;
        redirectUrl = oob.redirectUrl || null;
        jsAfter = oob.jsAfter || [];
        mutationTargets = oob.mutationTargets || [];
      } catch (e) {
        emit("heimdall:sse-error", { topic: state.topic, url, el: state.el, error: e });
        if (getConfig().debug) {
          console.error(`[Heimdall] SSE OOB processing error`, e);
        }
        return;
      }
      if (redirectUrl) {
        emit("heimdall:sse-redirect", {
          topic: state.topic,
          url,
          el: state.el,
          redirectUrl
        });
        dbg("sse redirecting", { topic: state.topic, redirectUrl });
        global.location.href = redirectUrl;
        return;
      }
      if (abortSwap) {
        emit("heimdall:sse-abort", { topic: state.topic, url, el: state.el, target: targetEl, swap: swapMode, reason: abortReason });
        dbg("sse swap aborted", { topic: state.topic, reason: abortReason, target: targetEl });
      }
      if (!abortSwap && swapMode !== "none" && targetEl) {
        const mainTpl = dom.parseHtmlToTemplate(html);
        dom.stripInvocationsFromFragment(mainTpl.content);
        dom.stripAbortsFromFragment(mainTpl.content);
        dom.stripRedirectsFromFragment(mainTpl.content);
        dom.stripHistoryFromFragment(mainTpl.content);
        dom.stripJsInvokeVoidFromFragment(mainTpl.content);
        dom.stripMutationsFromFragment(mainTpl.content);
        const swapResult = dom.applySwap(targetEl, mainTpl.content, swapMode, {
          kind: "sse",
          swapKind: "main",
          sourceEl: state.el,
          requestContext: null
        });
        const { didApply, appliedRoot } = swapResult;
        if (didApply && !getConfig().observeDom) {
          try {
            boot(appliedRoot || swapResult.target || targetEl);
          } catch {
          }
        }
      }
      dom.reconcileMutations(mutationTargets);
      dom.invokeJsInvokeVoidDirectives(jsAfter, {
        phase: "after",
        kind: "sse",
        topic: state.topic,
        event: state.eventName,
        url,
        target: targetEl,
        swap: swapMode
      });
      emit("heimdall:sse-message", {
        topic: state.topic,
        event: state.eventName,
        url,
        id: ev && ev.lastEventId ? String(ev.lastEventId) : null,
        bytes: data ? data.length : 0,
        el: state.el
      });
    }
    function getOrCreateSseConnection(topic) {
      const key = getConnectionKey(topic);
      let connection = _sseConnections.get(key);
      if (connection && !connection.closed)
        return connection;
      connection = {
        key,
        topic,
        url: null,
        es: null,
        closed: false,
        openedAt: Date.now(),
        lastMessageAt: 0,
        connecting: false,
        reconnectTimerId: null,
        retryCount: 0,
        connectAttempt: 0,
        paused: false,
        pauseReason: null,
        subscribers: /* @__PURE__ */ new Set(),
        eventHandlers: /* @__PURE__ */ new Map()
      };
      _sseConnections.set(key, connection);
      return connection;
    }
    function attachSse(el, options) {
      if (!el || !isElement(el))
        return null;
      const next = readSseConfig(el, options);
      const existing = _sseByElement.get(el);
      if (next.disabled) {
        if (existing)
          closeSseState(existing, "disabled");
        return null;
      }
      if (!next.topic) {
        if (existing && !existing.programmatic)
          closeSseState(existing, "topic-removed");
        return existing || null;
      }
      if (existing && !existing.closed) {
        if (existing.topic === next.topic) {
          const connection2 = existing.connection;
          existing.eventName = next.eventName;
          existing.target = next.target;
          existing.swap = next.swap;
          existing.programmatic = existing.programmatic || next.programmatic;
          existing.paused = !!(connection2 && connection2.paused);
          existing.pauseReason = connection2 ? connection2.pauseReason : null;
          if (connection2) {
            ensureConnectionEventListener(connection2, existing.eventName);
            pruneConnectionEventListeners(connection2);
            if (connection2.paused)
              resumeSseConnection(connection2, "config-updated");
            else
              connectSseConnection(connection2);
          }
          return existing;
        }
        closeSseState(existing, "topic-changed");
      }
      if (!("EventSource" in global)) {
        if (getConfig().debug) {
          console.warn(`[Heimdall] EventSource not available; SSE disabled.`, el);
        }
        return null;
      }
      const connection = getOrCreateSseConnection(next.topic);
      const state = {
        el,
        topic: next.topic,
        url: connection.url,
        eventName: next.eventName,
        target: next.target,
        swap: next.swap,
        closed: false,
        paused: !!connection.paused,
        pauseReason: connection.pauseReason || null,
        programmatic: next.programmatic,
        connection
      };
      _sseByElement.set(el, state);
      _sseStates.add(state);
      connection.subscribers.add(state);
      if (connection.es)
        ensureConnectionEventListener(connection, state.eventName);
      if (connection.paused)
        resumeSseConnection(connection, "subscriber-added");
      else
        connectSseConnection(connection);
      return state;
    }
    function bootSse(root) {
      const scope = isElement(root) ? root : document;
      if (isElement(root) && (_sseByElement.get(root) || matchesTriggerAttr(root, "heimdall-sse") || matchesTriggerAttr(root, "heimdall-sse-topic"))) {
        attachSse(root);
      }
      for (const el of scope.querySelectorAll("[heimdall-sse],[heimdall-sse-topic]"))
        attachSse(el);
    }
    let _sseSweepInstalled = false;
    let _sseGlobalEventsInstalled = false;
    function installSseGlobalEvents() {
      if (_sseGlobalEventsInstalled)
        return;
      _sseGlobalEventsInstalled = true;
      if (global && typeof global.addEventListener === "function") {
        global.addEventListener("offline", () => {
          pauseAllSse("offline");
        });
        global.addEventListener("online", () => {
          resumePausedSse("online");
        });
      }
      document.addEventListener("visibilitychange", () => {
        if (!getConfig().ssePauseWhenHidden)
          return;
        if (document.hidden) {
          pauseAllSse("hidden");
          return;
        }
        resumePausedSse("visible");
        try {
          bootSse(document);
        } catch {
        }
      });
    }
    function installSseSweeper() {
      if (_sseSweepInstalled)
        return;
      _sseSweepInstalled = true;
      installSseGlobalEvents();
      const sweepIntervalMs = getConfig().sseSweepIntervalMs || 5e3;
      setInterval(() => {
        const pauseReason = getPauseReason();
        if (pauseReason) {
          pauseAllSse(pauseReason);
          return;
        }
        for (const state of Array.from(_sseStates)) {
          validateStateElement(state);
        }
        for (const connection of Array.from(_sseConnections.values())) {
          if (!connection || connection.closed)
            continue;
          if (!validateSseConnection(connection))
            continue;
          if (connection.paused) {
            resumeSseConnection(connection, "sweep");
            continue;
          }
          connectSseConnection(connection);
        }
      }, sweepIntervalMs);
    }
    function sseConnect(topic, options) {
      options = options || {};
      const el = options.element || document.body;
      if (!isElement(el))
        throw new Error("Heimdall.sse.connect requires an element (options.element).");
      const state = attachSse(el, {
        topic,
        target: options.target,
        swap: options.swap,
        event: options.event,
        disable: options.disable,
        programmatic: true
      });
      return {
        close: () => closeSseState(state, "manual"),
        get topic() {
          return state ? state.topic : null;
        },
        get url() {
          return state ? getStateUrl(state) : null;
        }
      };
    }
    function sseDisconnect(element) {
      const el = resolveTarget(element, null);
      if (!el)
        return;
      const state = _sseByElement.get(el);
      if (state) closeSseState(state, "manual");
    }
    function sseDisconnectAll() {
      for (const state of Array.from(_sseStates)) {
        closeSseState(state, "manual-all");
      }
    }
    return {
      bootSse,
      installSseSweeper,
      sseConnect,
      sseDisconnect,
      sseDisconnectAll
    };
  }

  // core/time-localization.js
  var TIME_ATTRIBUTE = "heimdall-time";
  var FORMAT_ATTRIBUTE = "heimdall-time-format";
  var DEFAULT_FORMAT = "G";
  var MAXIMUM_FORMAT_LENGTH = 256;
  var MAXIMUM_CACHE_ENTRIES = 128;
  var STANDARD_FORMATS = /* @__PURE__ */ new Set(["d", "D", "t", "T", "g", "G"]);
  function createTimeLocalization({ global, emitLifecycle, dbg }) {
    const processedElements = /* @__PURE__ */ new WeakMap();
    const formatterCache = /* @__PURE__ */ new Map();
    const numberFormatterCache = /* @__PURE__ */ new Map();
    const parsedFormatCache = /* @__PURE__ */ new Map();
    let browserTimeZone = null;
    function setBoundedCache(cache, key, value) {
      if (cache.size >= MAXIMUM_CACHE_ENTRIES)
        cache.clear();
      cache.set(key, value);
      return value;
    }
    function getBrowserTimeZone() {
      if (browserTimeZone)
        return browserTimeZone;
      browserTimeZone = new Intl.DateTimeFormat().resolvedOptions().timeZone;
      if (!browserTimeZone)
        throw new Error("The browser did not provide a local timezone.");
      return browserTimeZone;
    }
    function findLanguage(element) {
      if (!element || typeof element.closest !== "function")
        return null;
      const languageElement = element.closest("[lang]");
      if (!languageElement)
        return null;
      const language = String(languageElement.getAttribute("lang") || "").trim();
      return language || null;
    }
    function resolveLocale(element, contextElement) {
      return findLanguage(element) || findLanguage(contextElement) || String(document.documentElement && document.documentElement.lang || "").trim() || String(global.navigator && global.navigator.language || "").trim() || "en-US";
    }
    function formatter(locale, timeZone, options) {
      const normalizedOptions = Object.assign({
        calendar: "gregory",
        timeZone
      }, options || {});
      const key = JSON.stringify([locale, normalizedOptions]);
      const cached = formatterCache.get(key);
      if (cached)
        return cached;
      return setBoundedCache(
        formatterCache,
        key,
        new Intl.DateTimeFormat(locale, normalizedOptions)
      );
    }
    function numberFormatter(locale, minimumIntegerDigits) {
      const key = `${locale}\0${minimumIntegerDigits}`;
      const cached = numberFormatterCache.get(key);
      if (cached)
        return cached;
      return setBoundedCache(
        numberFormatterCache,
        key,
        new Intl.NumberFormat(locale, {
          minimumIntegerDigits,
          maximumFractionDigits: 0,
          useGrouping: false
        })
      );
    }
    function formatInteger(value, locale, minimumIntegerDigits) {
      return numberFormatter(locale, minimumIntegerDigits).format(value);
    }
    function formatPart(date, locale, timeZone, options, partName) {
      const part = formatter(locale, timeZone, options).formatToParts(date).find((candidate) => candidate.type === partName);
      if (!part)
        throw new Error(`The browser could not format date/time part '${partName}'.`);
      return part.value;
    }
    function getZonedFields(date, timeZone) {
      const parts = formatter("en-US-u-ca-gregory-nu-latn", timeZone, {
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        hourCycle: "h23",
        minute: "2-digit",
        second: "2-digit"
      }).formatToParts(date);
      const fields = {};
      for (const part of parts) {
        if (part.type === "year" || part.type === "month" || part.type === "day" || part.type === "hour" || part.type === "minute" || part.type === "second") {
          fields[part.type] = Number(part.value);
        }
      }
      for (const name of ["year", "month", "day", "hour", "minute", "second"]) {
        if (!Number.isFinite(fields[name]))
          throw new Error(`The browser could not resolve local date/time field '${name}'.`);
      }
      return fields;
    }
    function getOffsetMinutes(date, timeZone, fields) {
      const wallClockAsUtc = /* @__PURE__ */ new Date(0);
      wallClockAsUtc.setUTCFullYear(fields.year, fields.month - 1, fields.day);
      wallClockAsUtc.setUTCHours(fields.hour, fields.minute, fields.second, 0);
      const instantWithoutMilliseconds = date.getTime() - date.getUTCMilliseconds();
      return Math.round((wallClockAsUtc.getTime() - instantWithoutMilliseconds) / 6e4);
    }
    function formatOffset(date, timeZone, count, fields) {
      const offsetMinutes = getOffsetMinutes(date, timeZone, fields);
      const sign = offsetMinutes < 0 ? "-" : "+";
      const absoluteMinutes = Math.abs(offsetMinutes);
      const hours = Math.floor(absoluteMinutes / 60);
      const minutes = absoluteMinutes % 60;
      if (count === 1)
        return `${sign}${hours}`;
      if (count === 2)
        return `${sign}${String(hours).padStart(2, "0")}`;
      return `${sign}${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}`;
    }
    function isTokenLetter(value) {
      return value === "d" || value === "M" || value === "y" || value === "h" || value === "H" || value === "m" || value === "s" || value === "t" || value === "f" || value === "z";
    }
    function isSupportedToken(value, count) {
      switch (value) {
        case "d":
        case "M":
        case "y":
          return count >= 1 && count <= 4;
        case "h":
        case "H":
        case "m":
        case "s":
        case "t":
          return count >= 1 && count <= 2;
        case "f":
        case "z":
          return count >= 1 && count <= 3;
        default:
          return false;
      }
    }
    function isLetter(value) {
      return value.toLocaleUpperCase() !== value.toLocaleLowerCase();
    }
    function unsupportedFormat(format, token) {
      return new RangeError(
        `Local time format '${format}' contains unsupported token '${token}'. Use d, D, t, T, g, or G, or a supported custom date/time format.`
      );
    }
    function parseCustomFormat(format) {
      const cached = parsedFormatCache.get(format);
      if (cached)
        return cached;
      if (format.length > MAXIMUM_FORMAT_LENGTH)
        throw new RangeError(`Local time formats cannot exceed ${MAXIMUM_FORMAT_LENGTH} characters.`);
      const segments = [];
      let literal = "";
      function flushLiteral() {
        if (!literal)
          return;
        segments.push({ type: "literal", value: literal });
        literal = "";
      }
      for (let index = 0; index < format.length; ) {
        const current = format[index];
        if (current === "'" || current === '"') {
          const quote = current;
          index++;
          let closed = false;
          while (index < format.length) {
            const quoted = format[index];
            if (quoted === "\\") {
              if (index + 1 >= format.length)
                break;
              literal += format[index + 1];
              index += 2;
              continue;
            }
            if (quoted === quote) {
              index++;
              closed = true;
              break;
            }
            literal += quoted;
            index++;
          }
          if (!closed)
            throw new RangeError("A local time format contains an unterminated quoted literal.");
          continue;
        }
        if (current === "\\") {
          if (index + 1 >= format.length)
            throw new RangeError("A local time format cannot end with an escape character.");
          literal += format[index + 1];
          index += 2;
          continue;
        }
        if (current === "%") {
          if (index + 1 >= format.length)
            throw new RangeError("A local time format cannot end with '%'.");
          const escapedToken = format[index + 1];
          if (!isSupportedToken(escapedToken, 1))
            throw unsupportedFormat(format, escapedToken);
          flushLiteral();
          segments.push({ type: "token", value: escapedToken, count: 1 });
          index += 2;
          continue;
        }
        if (isTokenLetter(current)) {
          let count = 1;
          while (index + count < format.length && format[index + count] === current)
            count++;
          if (!isSupportedToken(current, count))
            throw unsupportedFormat(format, current);
          flushLiteral();
          segments.push({ type: "token", value: current, count });
          index += count;
          continue;
        }
        if (isLetter(current))
          throw unsupportedFormat(format, current);
        literal += current;
        index++;
      }
      flushLiteral();
      return setBoundedCache(parsedFormatCache, format, segments);
    }
    function formatStandard(date, format, locale, timeZone) {
      switch (format) {
        case "d":
          return formatter(locale, timeZone, { dateStyle: "short" }).format(date);
        case "D":
          return formatter(locale, timeZone, { dateStyle: "full" }).format(date);
        case "t":
          return formatter(locale, timeZone, { timeStyle: "short" }).format(date);
        case "T":
          return formatter(locale, timeZone, { timeStyle: "medium" }).format(date);
        case "g":
          return formatter(locale, timeZone, { dateStyle: "short", timeStyle: "short" }).format(date);
        case "G":
          return formatter(locale, timeZone, { dateStyle: "short", timeStyle: "medium" }).format(date);
        default:
          throw unsupportedFormat(format, format);
      }
    }
    function formatCustomToken(date, token, locale, timeZone, fields) {
      const { value, count } = token;
      switch (value) {
        case "d":
          if (count <= 2)
            return formatInteger(fields.day, locale, count);
          return formatPart(date, locale, timeZone, {
            weekday: count === 3 ? "short" : "long"
          }, "weekday");
        case "M":
          if (count <= 2)
            return formatInteger(fields.month, locale, count);
          return formatPart(date, locale, timeZone, {
            month: count === 3 ? "short" : "long"
          }, "month");
        case "y":
          if (count <= 2)
            return formatInteger(fields.year % 100, locale, count);
          return formatInteger(fields.year, locale, count);
        case "H":
          return formatInteger(fields.hour, locale, count);
        case "h": {
          const twelveHour = fields.hour % 12 || 12;
          return formatInteger(twelveHour, locale, count);
        }
        case "m":
          return formatInteger(fields.minute, locale, count);
        case "s":
          return formatInteger(fields.second, locale, count);
        case "t": {
          const dayPeriod = formatPart(date, locale, timeZone, {
            hour: "numeric",
            hourCycle: "h12"
          }, "dayPeriod");
          return count === 1 ? Array.from(dayPeriod)[0] || "" : dayPeriod;
        }
        case "f":
          return String(date.getUTCMilliseconds()).padStart(3, "0").slice(0, count);
        case "z":
          return formatOffset(date, timeZone, count, fields);
        default:
          throw unsupportedFormat(token.value, token.value);
      }
    }
    function formatLocalTime(date, format, locale, timeZone) {
      if (STANDARD_FORMATS.has(format))
        return formatStandard(date, format, locale, timeZone);
      if (format.length === 1)
        throw unsupportedFormat(format, format);
      const segments = parseCustomFormat(format);
      const fields = getZonedFields(date, timeZone);
      let output = "";
      for (const segment of segments) {
        output += segment.type === "literal" ? segment.value : formatCustomToken(date, segment, locale, timeZone, fields);
      }
      return output;
    }
    function parseAbsoluteTime(value) {
      const normalized = String(value || "").trim();
      const absoluteIso = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,3})?(?:Z|[+-]\d{2}:\d{2})$/i;
      if (!absoluteIso.test(normalized))
        throw new RangeError("heimdall-time must contain an ISO timestamp with Z or an explicit offset.");
      const milliseconds = Date.parse(normalized);
      if (!Number.isFinite(milliseconds))
        throw new RangeError(`heimdall-time contains an invalid timestamp '${normalized}'.`);
      return { value: normalized, date: new Date(milliseconds) };
    }
    function signatureFor(value, format, timeZone, locale) {
      return `${value}\0${format}\0${timeZone}\0${locale}`;
    }
    function collectCandidates(root) {
      const candidates = [];
      if (isElement(root) && root.hasAttribute(TIME_ATTRIBUTE))
        candidates.push(root);
      if (root && typeof root.querySelectorAll === "function") {
        for (const element of root.querySelectorAll(`[${TIME_ATTRIBUTE}]`))
          candidates.push(element);
      }
      return candidates;
    }
    function localizeElement(element, options) {
      const rawValue = element.getAttribute(TIME_ATTRIBUTE);
      const rawFormat = element.getAttribute(FORMAT_ATTRIBUTE) || DEFAULT_FORMAT;
      let timeZone;
      let locale;
      let signature;
      try {
        timeZone = getBrowserTimeZone();
        locale = resolveLocale(element, options.contextElement);
        signature = signatureFor(rawValue, rawFormat, timeZone, locale);
        const existing = processedElements.get(element);
        if (existing && existing.signature === signature)
          return false;
        const detail = {
          element,
          value: rawValue,
          format: rawFormat,
          timeZone,
          locale,
          text: null,
          origin: options.origin || "boot",
          kind: options.kind || null
        };
        if (!emitLifecycle(element, "heimdall:time-before", detail, { cancelable: true })) {
          processedElements.set(element, { signature, status: "cancelled" });
          return false;
        }
        const parsed = parseAbsoluteTime(detail.value);
        const effectiveFormat = String(detail.format || DEFAULT_FORMAT);
        const effectiveTimeZone = String(detail.timeZone || timeZone);
        const effectiveLocale = String(detail.locale || locale);
        const text = detail.text == null ? formatLocalTime(parsed.date, effectiveFormat, effectiveLocale, effectiveTimeZone) : String(detail.text);
        element.textContent = text;
        processedElements.set(element, { signature, status: "localized" });
        emitLifecycle(element, "heimdall:time-after", {
          ...detail,
          value: parsed.value,
          format: effectiveFormat,
          timeZone: effectiveTimeZone,
          locale: effectiveLocale,
          text
        });
        return true;
      } catch (error) {
        if (signature)
          processedElements.set(element, { signature, status: "error" });
        emitLifecycle(element, "heimdall:time-error", {
          element,
          value: rawValue,
          format: rawFormat,
          timeZone: timeZone || null,
          locale: locale || null,
          error,
          origin: options.origin || "boot",
          kind: options.kind || null
        });
        dbg("local time formatting failed", { element, error });
        return false;
      }
    }
    function localize(root, options = {}) {
      let count = 0;
      for (const element of collectCandidates(root)) {
        if (localizeElement(element, options))
          count++;
      }
      return count;
    }
    return {
      localize
    };
  }

  // heimdall.entry.js
  (function(global) {
    "use strict";
    const API_VERSION = 1;
    const DEFAULT_BASE_PATH = "/__heimdall";
    const DEFAULT_CONTENT_ENDPOINT = `${DEFAULT_BASE_PATH}/v${API_VERSION}/content/actions`;
    const DEFAULT_CSRF_ENDPOINT = `${DEFAULT_BASE_PATH}/v${API_VERSION}/csrf`;
    const DEFAULT_BIFROST_ENDPOINT = `${DEFAULT_BASE_PATH}/v${API_VERSION}/bifrost`;
    const DEFAULT_BIFROST_TOKEN_ENDPOINT = `${DEFAULT_BASE_PATH}/v${API_VERSION}/bifrost/token`;
    const ACTION_HEADER = "X-Heimdall-Content-Action";
    const CSRF_HEADER = "RequestVerificationToken";
    const CLIENT_INFO_HEADER = "X-Heimdall-Client-Info";
    const runtimeRef = { current: null };
    const getRuntimeConfig = () => runtimeRef.current && runtimeRef.current.config;
    const { payloadBindingFromElement } = createPayloadResolver(global);
    const { emit, emitLifecycle, dbg } = createDiagnostics(getRuntimeConfig);
    const coordinator = createRequestCoordinator({
      global,
      dbg
    });
    const requestHeaders = createRequestHeadersRuntime({
      global,
      getConfig: getRuntimeConfig
    });
    const jsInvokeVoid = createJsInvokeVoidRuntime({
      global,
      emit,
      dbg,
      getConfig: getRuntimeConfig
    });
    const timeLocalization = createTimeLocalization({
      global,
      emitLifecycle,
      dbg
    });
    const busyState = createBusyStateManager();
    const clientInfo = createClientInfoRuntime({
      global,
      getConfig: getRuntimeConfig,
      emitLifecycle,
      dbg
    });
    const historyRuntime = createHistoryRuntime({
      global,
      emitLifecycle,
      dbg
    });
    const mutations = createMutationRuntime({
      global,
      emitLifecycle,
      dbg,
      boot: (root) => boot(root),
      busyState
    });
    const dom = createDomPipeline({
      getConfig: getRuntimeConfig,
      boot: (root) => boot(root),
      dbg,
      emitLifecycle,
      jsInvokeVoid,
      timeLocalization,
      mutations
    });
    const {
      clearBifrostSubscribeToken,
      clearCsrfToken,
      ensureBifrostSubscribeToken,
      ensureCsrfToken
    } = createSecurityTokens({
      global,
      getConfig: getRuntimeConfig,
      emit,
      emitLifecycle,
      dbg,
      safeText,
      resolveRequestHeaders: requestHeaders.resolve,
      csrfHeader: CSRF_HEADER,
      defaultBifrostTokenEndpoint: DEFAULT_BIFROST_TOKEN_ENDPOINT
    });
    const {
      invoke,
      runActionFromElement
    } = createActionInvoker({
      global,
      getConfig: getRuntimeConfig,
      ensureCsrfToken,
      clearCsrfToken,
      emit,
      emitLifecycle,
      dbg,
      payloadBindingFromElement,
      boot: (root) => boot(root),
      dom,
      historyRuntime,
      coordinator,
      busyState,
      resolveRequestHeaders: requestHeaders.resolve,
      mergeRequestHeaders: requestHeaders.merge,
      getClientInfoHeader: clientInfo.getHeaderValue,
      actionHeader: ACTION_HEADER,
      csrfHeader: CSRF_HEADER,
      clientInfoHeader: CLIENT_INFO_HEADER
    });
    const {
      handleChange,
      handleClick,
      handleFocusOut,
      handleInput,
      handleKeydown,
      handleMouseOut,
      handleMouseOver,
      handleSubmit
    } = createEventDelegates({
      getConfig: getRuntimeConfig,
      runActionFromElement
    });
    const {
      bootLoads,
      bootPoll,
      bootScroll,
      bootVisible,
      matchesTriggerAttr
    } = createBootTriggers({
      global,
      getConfig: getRuntimeConfig,
      runActionFromElement
    });
    const pageLifecycle = createPageLifecycleRuntime({
      global,
      runActionFromElement,
      emit
    });
    const {
      bootSse,
      installSseSweeper,
      sseConnect,
      sseDisconnect,
      sseDisconnectAll
    } = createSseRuntime({
      global,
      getConfig: getRuntimeConfig,
      emit,
      dbg,
      dom,
      boot: (root) => boot(root),
      clearBifrostSubscribeToken,
      ensureBifrostSubscribeToken,
      matchesTriggerAttr,
      defaultBifrostEndpoint: DEFAULT_BIFROST_ENDPOINT
    });
    function boot(root) {
      timeLocalization.localize(root, { origin: "boot" });
      bootLoads(root);
      bootVisible(root);
      bootScroll(root);
      bootPoll(root);
      bootSse(root);
    }
    const Heimdall = createHeimdallRuntime({
      global,
      apiVersion: API_VERSION,
      defaultBasePath: DEFAULT_BASE_PATH,
      defaultContentEndpoint: DEFAULT_CONTENT_ENDPOINT,
      defaultCsrfEndpoint: DEFAULT_CSRF_ENDPOINT,
      defaultBifrostTokenEndpoint: DEFAULT_BIFROST_TOKEN_ENDPOINT,
      defaultBifrostEndpoint: DEFAULT_BIFROST_ENDPOINT,
      invoke,
      boot,
      onReady,
      clearCsrfToken,
      sseConnect,
      sseDisconnect,
      sseDisconnectAll,
      handlers: {
        handleChange,
        handleClick,
        handleFocusOut,
        handleInput,
        handleKeydown,
        handleMouseOut,
        handleMouseOver,
        handleSubmit
      },
      installPageLifecycle: pageLifecycle.install,
      installSseSweeper,
      dbg,
      onRuntimeCreated: (runtime) => {
        runtimeRef.current = runtime;
      }
    });
  })(window);
})();
