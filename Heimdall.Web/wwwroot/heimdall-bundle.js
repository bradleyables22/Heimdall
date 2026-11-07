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
    function abortError2() {
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
        return Promise.reject(abortError2());
      return new Promise((resolve2, reject) => {
        let settled = false;
        const onAbort = () => {
          if (settled)
            return;
          settled = true;
          reject(abortError2());
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
  function createInvocationInvoker({
    global,
    attributes,
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
    plugins,
    resolveRequestHeaders,
    mergeRequestHeaders,
    getClientInfoHeader,
    invocationHeader,
    csrfHeader,
    clientInfoHeader
  }) {
    const { get: getAttr2, truthy: truthyAttr } = attributes;
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
      const configuredSync = options.sync != null ? options.sync : sourceElement ? getAttr2(sourceElement, "heimdall-sync") : null;
      const configuredGroup = options.syncGroup != null ? options.syncGroup : sourceElement ? getAttr2(sourceElement, "heimdall-sync-group") : null;
      let currentInvocationId = actionId;
      const context = {
        requestId: ++nextRequestId,
        attempt: 0,
        trigger: options.triggerName || null,
        sourceElement,
        target: options.target,
        fallbackTarget: options.fallbackTarget || null,
        swap: options.swap || "inner",
        endpoint: options.endpoint || config.endpoints.contentInvocations || config.endpoints.contentActions,
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
      Object.defineProperties(context, {
        invocationId: {
          configurable: true,
          enumerable: true,
          get() {
            return currentInvocationId;
          },
          set(value) {
            currentInvocationId = value;
          }
        },
        // Compatibility alias for applications migrating from v1.
        actionId: {
          configurable: true,
          enumerable: true,
          get() {
            return currentInvocationId;
          },
          set(value) {
            currentInvocationId = value;
          }
        }
      });
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
        invocationId: context.invocationId,
        actionId: context.invocationId,
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
      url.searchParams.set("invocation", context.invocationId);
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
        [invocationHeader]: context.invocationId
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
            kind: "content-invocation",
            url: url.toString(),
            method: "POST",
            invocationId: context.invocationId,
            actionId: context.invocationId,
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
          const err = new Error(`Heimdall payload is not JSON-serializable for invocation '${context.invocationId}'.`);
          err.cause = e;
          emit("heimdall:error", {
            invocationId: context.invocationId,
            actionId: context.invocationId,
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
        invocationId: context.invocationId,
        actionId: context.invocationId,
        payload: context.payload,
        target: context.target,
        swap: context.swap,
        endpoint: context.request.url
      });
      dbg("invoke ->", context.invocationId, {
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
          invocationId: context.invocationId,
          actionId: context.invocationId,
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
          kind: "content-invocation",
          invocationId: context.invocationId,
          actionId: context.invocationId,
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
          invocationId: context.invocationId,
          actionId: context.invocationId,
          payload: context.payload,
          target: context.target,
          swap: context.swap,
          endpoint: context.request.url,
          status: response.status,
          url: redirectUrl2
        });
        dbg("redirecting", { actionId: context.invocationId, url: redirectUrl2 });
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
      let pluginCommands = { before: [], after: [] };
      const pluginContext = {
        sourceElement: context.sourceElement,
        invocationId: context.invocationId,
        origin: "invocation",
        signal: context.signal,
        isCurrent: () => coordinator.isCurrent(context)
      };
      if (response.ok) {
        pluginCommands = plugins.prepareCommands(html);
        html = pluginCommands.html;
        if (pluginCommands.before.length)
          await plugins.runCommands(pluginCommands.before, pluginContext);
        if (!coordinator.isCurrent(context))
          return coordinator.getCancellationResult(context);
        const oob = dom.processOob(html, context.sourceElement, {
          kind: "invocation",
          invocationId: context.invocationId,
          actionId: context.invocationId,
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
          invocationId: context.invocationId,
          actionId: context.invocationId,
          payload: context.payload,
          target: context.target,
          swap: context.swap,
          endpoint: context.request.url,
          status: response.status,
          url: redirectUrl
        });
        dbg("redirecting", { actionId: context.invocationId, url: redirectUrl });
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
          invocationId: context.invocationId,
          actionId: context.invocationId,
          payload: context.payload,
          target: context.target,
          swap: context.swap,
          endpoint: context.request.url,
          status: response.status,
          reason: abortReason
        });
        dbg("swap aborted", { actionId: context.invocationId, reason: abortReason, target: context.target });
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
            kind: "invocation",
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
        if (pluginCommands.after.length)
          await plugins.runCommands(pluginCommands.after, pluginContext);
        if (!coordinator.isCurrent(context))
          return coordinator.getCancellationResult(context);
        dom.invokeJsInvokeVoidDirectives(jsAfter, {
          phase: "after",
          kind: "invocation",
          invocationId: context.invocationId,
          actionId: context.invocationId,
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
          invocationId: context.invocationId,
          actionId: context.invocationId,
          payload: context.payload,
          target: context.target,
          swap: context.swap,
          status: response.status,
          body: html
        });
      } else {
        emit("heimdall:after", {
          invocationId: context.invocationId,
          actionId: context.invocationId,
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
      dbg("invoke <-", context.invocationId, result);
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
    function getCommonOptions(el, triggerName, extraOptions) {
      const target = getAttr2(el, "heimdall-content-target") || el;
      const swap = getAttr2(el, "heimdall-content-swap") || "inner";
      const sync = getAttr2(el, "heimdall-sync");
      const syncGroup = getAttr2(el, "heimdall-sync-group");
      let payloadResolution = extraOptions && Object.prototype.hasOwnProperty.call(extraOptions, "payload") ? { value: extraOptions.payload, binding: null } : payloadBindingFromElement(el);
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
    async function runInvocationFromElement(el, actionId, triggerName, extraOptions) {
      if (!el || !actionId)
        return;
      if (el.hasAttribute("disabled") || el.getAttribute("aria-disabled") === "true")
        return;
      try {
        const pending = plugins?.ready(el);
        if (pending) {
          await pending;
          if (!el.isConnected) return;
        }
        const { target, swap, payload, payloadBinding, sync, syncGroup } = getCommonOptions(el, triggerName, extraOptions);
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
        return await invoke(actionId, payload, opts);
      } catch (err) {
        console.error(err);
      }
    }
    return {
      invoke,
      runInvocationFromElement,
      runActionFromElement: runInvocationFromElement
    };
  }

  // core/attributes.js
  var builtins = [
    ...[
      "click",
      "submit",
      "input",
      "change",
      "keydown",
      "blur",
      "hover",
      "load",
      "visible",
      "scroll",
      "document-visible",
      "online"
    ].map((name) => [`heimdall-content-${name}`, "trigger"]),
    ...["target", "swap", "disable"].map((name) => [`heimdall-content-${name}`, "content"]),
    ...["payload", "payload-from", "payload-ref"].map((name) => [`heimdall-${name}`, "payload"]),
    ...[
      "prevent-default",
      "ignore",
      "scope",
      "sync",
      "sync-group",
      "debounce",
      "key",
      "hover-delay",
      "visible-once",
      "scroll-threshold",
      "poll"
    ].map((name) => [`heimdall-${name}`, "modifier"]),
    ...["sse", "sse-topic", "sse-target", "sse-swap", "sse-event", "sse-disable"].map((name) => [`heimdall-${name}`, "sse"]),
    ["heimdall-time", "time"],
    ["heimdall-time-format", "time"],
    ["data-heimdall-state", "state"],
    ["data-heimdall-state-*", "state"]
  ];
  var nativeNames = /* @__PURE__ */ new Set([
    "id",
    "class",
    "style",
    "name",
    "value",
    "type",
    "disabled",
    "hidden",
    "checked",
    "selected",
    "multiple",
    "required",
    "readonly",
    "lang",
    "dir",
    "role",
    "command",
    "commandfor",
    "action",
    "method",
    "enctype",
    "form",
    "href",
    "src"
  ]);
  function validateName(name, prefix) {
    const value = prefix ? name?.slice(0, -1) : name;
    if (typeof name !== "string" || prefix && !name.endsWith("-*") || !/^[a-z][a-z0-9_-]*$/.test(value || "") || /^on[a-z]+$/.test(name) || name.startsWith("aria-") || nativeNames.has(name))
      throw new TypeError(`Invalid or browser-owned attribute name '${name}'.`);
  }
  function overlaps(a, b) {
    const ap = a.endsWith("*"), bp = b.endsWith("*");
    if (ap && bp) return a.slice(0, -1).startsWith(b.slice(0, -1)) || b.slice(0, -1).startsWith(a.slice(0, -1));
    if (ap) return b.startsWith(a.slice(0, -1));
    if (bp) return a.startsWith(b.slice(0, -1));
    return a === b;
  }
  function createAttributeRegistry() {
    let definitions = new Map(builtins.map(([name, category]) => [
      name,
      Object.freeze({ name, category, kind: name.endsWith("*") ? "prefix" : "attribute", owner: "heimdall" })
    ]));
    let layers = /* @__PURE__ */ new Map();
    let depth = 0, dirty = false;
    let recognizedPatterns = null;
    const listeners = /* @__PURE__ */ new Set(), cache = /* @__PURE__ */ new Map();
    function effective(stack) {
      for (let index = (stack?.length || 0) - 1; index >= 0; index--) {
        const record = stack[index];
        if (definitions.get(record.definition.name) === record.definition) return record;
      }
      return null;
    }
    function changed() {
      cache.clear();
      recognizedPatterns = null;
      dirty = true;
      if (!depth) {
        dirty = false;
        for (const listener of listeners) listener();
      }
    }
    function transaction(callback) {
      const previousDefinitions = new Map(definitions);
      const previousLayers = new Map(Array.from(layers, ([name, stack]) => [name, [...stack]]));
      const previousDirty = dirty;
      depth++;
      try {
        return callback();
      } catch (error) {
        definitions = previousDefinitions;
        layers = previousLayers;
        dirty = previousDirty;
        cache.clear();
        recognizedPatterns = null;
        throw error;
      } finally {
        depth--;
        if (!depth && dirty) changed();
      }
    }
    function aliasesFor(definition) {
      return Array.from(layers, ([name, stack]) => effective(stack)?.definition === definition ? name : null).filter(Boolean);
    }
    function list() {
      return Object.freeze(Array.from(definitions.values(), (definition) => Object.freeze({
        ...definition,
        aliases: Object.freeze(aliasesFor(definition))
      })));
    }
    function alias(mappings, options = {}, owner = null) {
      if (!mappings || typeof mappings !== "object" || Array.isArray(mappings))
        throw new TypeError("Attribute aliases must be a map of original names to aliases.");
      const records = [];
      transaction(() => {
        for (const [original, values] of Object.entries(mappings)) {
          const definition = definitions.get(original);
          if (!definition) throw new Error(`Unknown original attribute '${original}'.`);
          const names2 = typeof values === "string" ? [values] : values;
          if (!Array.isArray(names2)) throw new TypeError(`Aliases for '${original}' must be a string or array.`);
          for (const name of new Set(names2)) {
            validateName(name, definition.kind === "prefix");
            if (name === original) continue;
            for (const canonical of definitions.keys()) {
              if (overlaps(name, canonical)) throw new Error(`Alias '${name}' conflicts with original attribute '${canonical}'.`);
            }
            for (const [other, stack2] of layers) {
              const current = effective(stack2);
              if (!current || !overlaps(name, other)) continue;
              if (name !== other || current.definition !== definition && options.replace !== true)
                throw new Error(`Attribute alias '${name}' is already registered for '${current.definition.name}'.`);
            }
            const record = { definition, owner };
            const stack = layers.get(name) || [];
            stack.push(record);
            layers.set(name, stack);
            records.push([name, record]);
          }
        }
        if (records.length) changed();
      });
      let removed = false;
      return () => {
        if (removed) return;
        removed = true;
        let didRemove = false;
        for (const [name, record] of records) {
          const stack = layers.get(name);
          const index = stack?.indexOf(record) ?? -1;
          if (index < 0) continue;
          stack.splice(index, 1);
          if (!stack.length) layers.delete(name);
          didRemove = true;
        }
        if (didRemove) changed();
      };
    }
    function register(name, owner, aliases = []) {
      transaction(() => {
        validateName(name, false);
        if (definitions.has(name)) throw new Error(`Original attribute '${name}' is already registered.`);
        for (const canonical of definitions.keys()) {
          if (overlaps(name, canonical)) throw new Error(`Attribute '${name}' conflicts with '${canonical}'.`);
        }
        for (const [other, stack] of layers) {
          if (effective(stack) && overlaps(name, other)) throw new Error(`Attribute '${name}' conflicts with alias '${other}'.`);
        }
        definitions.set(name, Object.freeze({ name, category: "plugin-trigger", kind: "attribute", owner: owner.id }));
        alias({ [name]: aliases }, {}, owner);
        owner.attributes.add(name);
        changed();
      });
    }
    function removeOwner(owner) {
      let removed = false;
      for (const [name, definition] of definitions) {
        if (definition.owner === owner.id && owner.attributes?.has(name)) {
          definitions.delete(name);
          removed = true;
        }
      }
      for (const [name, stack] of layers) {
        const remaining = stack.filter((record) => record.owner !== owner && definitions.get(record.definition.name) === record.definition);
        if (remaining.length === stack.length) continue;
        if (remaining.length) layers.set(name, remaining);
        else layers.delete(name);
        removed = true;
      }
      if (removed) changed();
    }
    function names(original) {
      if (cache.has(original)) return cache.get(original);
      let definition = definitions.get(original), suffix = "";
      if (!definition) {
        definition = Array.from(definitions.values()).find((entry) => entry.kind === "prefix" && original.startsWith(entry.name.slice(0, -1)));
        if (definition) suffix = original.slice(definition.name.length - 1);
      }
      const concretePrefix = definition?.kind === "prefix" && original !== definition.name;
      const result = definition ? [definition.name, ...aliasesFor(definition)].map((name) => concretePrefix ? name.slice(0, -1) + suffix : name) : [original];
      const frozen = Object.freeze(result);
      if (cache.size >= 1024) cache.clear();
      cache.set(original, frozen);
      return frozen;
    }
    function get(element, original) {
      for (const name of names(original)) {
        const value = element.getAttribute(name);
        if (value != null) return value;
      }
      return null;
    }
    function has(element, original) {
      return names(original).some((name) => element.hasAttribute(name));
    }
    function selector(original) {
      if (original.endsWith("*")) throw new TypeError("Attribute prefixes require a concrete suffix for selectors.");
      return names(original).map((name) => `[${CSS.escape(name)}]`).join(",");
    }
    function truthy(element, original, fallback) {
      const value = get(element, original);
      if (value == null) return !!fallback;
      const normalized = value.toLowerCase().trim();
      if (["", "true", "1", "yes"].includes(normalized)) return true;
      if (["false", "0", "no"].includes(normalized)) return false;
      return !!fallback;
    }
    function int(element, original, fallback) {
      const value = get(element, original);
      const number = value == null || value === "" ? NaN : parseInt(value, 10);
      return Number.isFinite(number) ? number : fallback;
    }
    function recognizes(name) {
      recognizedPatterns || (recognizedPatterns = [...definitions.keys(), ...Array.from(layers.keys()).filter((alias2) => effective(layers.get(alias2)))]);
      return recognizedPatterns.some((pattern) => pattern.endsWith("*") ? name.startsWith(pattern.slice(0, -1)) : name === pattern);
    }
    const publicApi = Object.freeze({ list, alias, names, get, has, selector });
    return {
      publicApi,
      list,
      alias,
      register,
      removeOwner,
      transaction,
      names,
      get,
      has,
      selector,
      truthy,
      int,
      recognizes,
      onChange(callback) {
        listeners.add(callback);
        return () => listeners.delete(callback);
      },
      scoped(owner, assertInstalling) {
        return Object.freeze({ ...publicApi, alias(mappings, options) {
          assertInstalling();
          return alias(mappings, options, owner);
        } });
      }
    };
  }

  // core/boot-triggers.js
  function createBootTriggers({
    global,
    attributes,
    getConfig,
    runInvocationFromElement
  }) {
    const { get: getAttr2, int: intAttr, truthy: truthyAttr } = attributes;
    function matchesTriggerAttr(el, attr) {
      return isElement(el) && attributes.has(el, attr);
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
      const actionId = (getAttr2(el, "heimdall-content-load") || "").trim();
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
      runInvocationFromElement(el, actionId, "load").catch(() => {
      });
    }
    function bootLoads(root) {
      for (const el of candidates(root, attributes.selector("heimdall-content-load")))
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
          const actionId = getAttr2(el, "heimdall-content-visible");
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
          runInvocationFromElement(el, actionId, "visible").catch(() => {
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
      const actionId = (getAttr2(el, "heimdall-content-visible") || "").trim();
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
      for (const el of candidates(root, attributes.selector("heimdall-content-visible")))
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
      const actionId = (getAttr2(el, "heimdall-content-scroll") || "").trim();
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
          const currentActionId = getAttr2(el, "heimdall-content-scroll");
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
          runInvocationFromElement(el, currentActionId, "scroll").catch(() => {
          });
        });
      };
      _scrollStates.set(el, state);
      el.__heimdallScrollBound = true;
      el.addEventListener("scroll", state.handler, { passive: true });
    }
    function bootScroll(root) {
      for (const el of candidates(root, attributes.selector("heimdall-content-scroll")))
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
          await runInvocationFromElement(el, state.actionId, "load", { reason: "poll" });
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
      const actionId = (getAttr2(el, "heimdall-content-load") || "").trim();
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
      for (const el of candidates(root, attributes.selector("heimdall-poll")))
        reconcilePoll(el);
    }
    function reconcileAttributes(root) {
      for (const el of candidates(root, "*")) {
        if (_loadActions.has(el) || attributes.has(el, "heimdall-content-load")) reconcileLoad(el);
        if (_visibleStates.has(el) || attributes.has(el, "heimdall-content-visible")) reconcileVisible(el);
        if (_scrollStates.has(el) || attributes.has(el, "heimdall-content-scroll")) reconcileScroll(el);
        if (_pollStates.has(el) || attributes.has(el, "heimdall-poll")) reconcilePoll(el);
      }
    }
    return {
      bootLoads,
      bootPoll,
      bootScroll,
      bootVisible,
      reconcileAttributes,
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
  var DEFAULT_LIMITS = {
    maxKeysPerArea: 16,
    maxKeyLength: 128,
    maxValueLength: 1024,
    maxStorageChars: 2048,
    maxExtensionEntries: 16,
    maxExtensionChars: 1024,
    maxExtensionDepth: 4
  };
  var MAX_HEADER_LENGTH = 4096;
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
    function nonNegativeLimit(value, fallback) {
      const number = Number(value);
      return Number.isFinite(number) ? Math.max(0, Math.floor(number)) : fallback;
    }
    function normalizeLimits(config) {
      const configured = config?.clientInfoLimits;
      return {
        maxKeysPerArea: nonNegativeLimit(configured?.maxKeysPerArea, DEFAULT_LIMITS.maxKeysPerArea),
        maxKeyLength: nonNegativeLimit(configured?.maxKeyLength, DEFAULT_LIMITS.maxKeyLength),
        maxValueLength: nonNegativeLimit(configured?.maxValueLength, DEFAULT_LIMITS.maxValueLength),
        maxStorageChars: nonNegativeLimit(configured?.maxStorageChars, DEFAULT_LIMITS.maxStorageChars),
        maxExtensionEntries: nonNegativeLimit(configured?.maxExtensionEntries, DEFAULT_LIMITS.maxExtensionEntries),
        maxExtensionChars: nonNegativeLimit(configured?.maxExtensionChars, DEFAULT_LIMITS.maxExtensionChars),
        maxExtensionDepth: nonNegativeLimit(configured?.maxExtensionDepth, DEFAULT_LIMITS.maxExtensionDepth)
      };
    }
    function normalizeClientInfoConfig(config) {
      const raw = config?.clientInfo;
      const enabled = raw === true ? true : !!(raw && typeof raw === "object" && raw.enabled === true);
      const storage = raw && typeof raw === "object" && raw.storage && typeof raw.storage === "object" ? raw.storage : {};
      function keysFor(area) {
        if (!Array.isArray(storage[area]))
          return [];
        const seen = /* @__PURE__ */ new Set();
        const result = [];
        for (const rawKey of storage[area]) {
          const key = String(rawKey ?? "").trim();
          if (!key || seen.has(key))
            continue;
          seen.add(key);
          result.push(key);
        }
        return result;
      }
      return {
        enabled,
        local: keysFor("local"),
        session: keysFor("session"),
        limits: normalizeLimits(config)
      };
    }
    function emitClientInfoError(requestContext, detail) {
      const eventDetail = {
        phase: "client-info",
        invocationId: requestContext?.invocationId || requestContext?.actionId || null,
        actionId: requestContext?.actionId || null,
        requestId: requestContext?.requestId || null,
        attempt: requestContext?.attempt || 0,
        sourceElement: requestContext?.sourceElement || null,
        ...detail
      };
      if (typeof emitLifecycle === "function") {
        emitLifecycle(
          requestContext?.sourceElement || null,
          "heimdall:client-info-error",
          eventDetail
        );
      }
      if (typeof dbg === "function")
        dbg("client information value omitted", eventDetail);
    }
    function serializedLength(value) {
      try {
        return JSON.stringify(value).length;
      } catch {
        return Number.POSITIVE_INFINITY;
      }
    }
    function jsonDepth(value) {
      if (!value || typeof value !== "object")
        return 0;
      const values = Array.isArray(value) ? value : Object.values(value);
      if (values.length === 0)
        return 1;
      return 1 + Math.max(...values.map(jsonDepth));
    }
    function tryReadStorage(area, key, requestContext) {
      let storage;
      try {
        storage = global[area === "local" ? "localStorage" : "sessionStorage"];
      } catch (error) {
        emitClientInfoError(requestContext, {
          code: "storage-area-unavailable",
          area,
          key: null,
          errorName: error?.name || "StorageError"
        });
        return { available: false, value: null };
      }
      if (!storage || typeof storage.getItem !== "function") {
        emitClientInfoError(requestContext, {
          code: "storage-area-unavailable",
          area,
          key: null,
          errorName: "StorageUnavailable"
        });
        return { available: false, value: null };
      }
      try {
        return { available: true, value: storage.getItem(key) };
      } catch (error) {
        emitClientInfoError(requestContext, {
          code: "storage-read-failed",
          area,
          key,
          errorName: error?.name || "StorageError"
        });
        return { available: true, value: null };
      }
    }
    function collectStorage(clientInfoConfig, requestContext) {
      const limits = clientInfoConfig.limits;
      const storage = {};
      let hasValues = false;
      for (const area of ["local", "session"]) {
        const configuredKeys = clientInfoConfig[area];
        const areaValues = {};
        for (let index = 0; index < configuredKeys.length; index++) {
          const key = configuredKeys[index];
          if (index >= limits.maxKeysPerArea) {
            emitClientInfoError(requestContext, {
              code: "storage-key-limit",
              area,
              key,
              limit: limits.maxKeysPerArea
            });
            continue;
          }
          if (key.length > limits.maxKeyLength) {
            emitClientInfoError(requestContext, {
              code: "storage-key-too-long",
              area,
              key,
              limit: limits.maxKeyLength
            });
            continue;
          }
          const result = tryReadStorage(area, key, requestContext);
          if (!result.available || result.value == null)
            continue;
          if (result.value.length > limits.maxValueLength) {
            emitClientInfoError(requestContext, {
              code: "storage-value-too-long",
              area,
              key,
              limit: limits.maxValueLength
            });
            continue;
          }
          const candidate = {
            local: area === "local" ? { ...areaValues, [key]: result.value } : storage.local || {},
            session: area === "session" ? { ...areaValues, [key]: result.value } : storage.session || {}
          };
          if (area === "local")
            candidate.session = storage.session || {};
          else
            candidate.local = storage.local || {};
          if (serializedLength(candidate) > limits.maxStorageChars) {
            emitClientInfoError(requestContext, {
              code: "storage-size-limit",
              area,
              key,
              limit: limits.maxStorageChars
            });
            continue;
          }
          areaValues[key] = result.value;
          storage[area] = areaValues;
          hasValues = true;
        }
      }
      return hasValues ? storage : null;
    }
    function normalizeExtensions(info, requestContext, limits) {
      const source = info && info.extensions && typeof info.extensions === "object" ? info.extensions : {};
      const normalized = {};
      const entries = Object.entries(source);
      for (let index = 0; index < entries.length; index++) {
        const [key, value] = entries[index];
        if (index >= limits.maxExtensionEntries) {
          emitClientInfoError(requestContext, {
            code: "extension-entry-limit",
            area: null,
            key,
            limit: limits.maxExtensionEntries
          });
          continue;
        }
        if (jsonDepth(value) > limits.maxExtensionDepth) {
          emitClientInfoError(requestContext, {
            code: "extension-depth-limit",
            area: null,
            key,
            limit: limits.maxExtensionDepth
          });
          continue;
        }
        const candidate = { ...normalized, [key]: value };
        if (serializedLength(candidate) > limits.maxExtensionChars) {
          emitClientInfoError(requestContext, {
            code: "extension-size-limit",
            area: null,
            key,
            limit: limits.maxExtensionChars
          });
          continue;
        }
        normalized[key] = value;
      }
      return normalized;
    }
    function normalizeStorage(infoStorage, requestContext, limits) {
      const normalized = {};
      let hasValues = false;
      for (const area of ["local", "session"]) {
        const source = infoStorage && infoStorage[area] && typeof infoStorage[area] === "object" ? infoStorage[area] : {};
        const values = {};
        const entries = Object.entries(source);
        for (let index = 0; index < entries.length; index++) {
          const [key, value] = entries[index];
          if (index >= limits.maxKeysPerArea) {
            emitClientInfoError(requestContext, {
              code: "storage-key-limit",
              area,
              key,
              limit: limits.maxKeysPerArea
            });
            continue;
          }
          if (key.length > limits.maxKeyLength) {
            emitClientInfoError(requestContext, {
              code: "storage-key-too-long",
              area,
              key,
              limit: limits.maxKeyLength
            });
            continue;
          }
          if (typeof value !== "string") {
            emitClientInfoError(requestContext, {
              code: "storage-value-invalid",
              area,
              key,
              limit: limits.maxValueLength
            });
            continue;
          }
          if (value.length > limits.maxValueLength) {
            emitClientInfoError(requestContext, {
              code: "storage-value-too-long",
              area,
              key,
              limit: limits.maxValueLength
            });
            continue;
          }
          const candidate = {
            local: area === "local" ? { ...values, [key]: value } : normalized.local || {},
            session: area === "session" ? { ...values, [key]: value } : normalized.session || {}
          };
          if (serializedLength(candidate) > limits.maxStorageChars) {
            emitClientInfoError(requestContext, {
              code: "storage-size-limit",
              area,
              key,
              limit: limits.maxStorageChars
            });
            continue;
          }
          values[key] = value;
          normalized[area] = values;
          hasValues = true;
        }
      }
      return hasValues ? normalized : {};
    }
    function prepareHeaderInfo(info, clientInfoConfig, requestContext) {
      if (!info || typeof info !== "object")
        return info;
      const limits = clientInfoConfig.limits;
      if (info.storage && typeof info.storage === "object")
        info.storage = normalizeStorage(info.storage, requestContext, limits);
      if (Object.prototype.hasOwnProperty.call(info, "extensions"))
        info.extensions = normalizeExtensions(info, requestContext, limits);
      return info;
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
      const clientInfoConfig = normalizeClientInfoConfig(config);
      if (!config || !clientInfoConfig.enabled)
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
        invocationId: requestContext?.invocationId || requestContext?.actionId || null,
        actionId: requestContext?.actionId || null,
        requestId: requestContext?.requestId || null,
        attempt: requestContext?.attempt || 0,
        sourceElement: requestContext?.sourceElement || null,
        info: cloneInfo(cachedInfo)
      };
      const storage = collectStorage(clientInfoConfig, requestContext);
      if (storage)
        detail.info.storage = storage;
      const shouldInclude = typeof emitLifecycle !== "function" || emitLifecycle(
        requestContext?.sourceElement || null,
        "heimdall:client-info-before",
        detail,
        { cancelable: true }
      );
      if (!shouldInclude || detail.info == null)
        return null;
      try {
        const prepared = prepareHeaderInfo(detail.info, clientInfoConfig, requestContext);
        let serialized = JSON.stringify(prepared);
        if (serialized.length > MAX_HEADER_LENGTH && prepared && typeof prepared === "object") {
          if (prepared.extensions && Object.keys(prepared.extensions).length > 0) {
            delete prepared.extensions;
            emitClientInfoError(requestContext, {
              code: "header-size-limit",
              area: null,
              key: null,
              dropped: "extensions",
              limit: MAX_HEADER_LENGTH
            });
            serialized = JSON.stringify(prepared);
          }
          if (serialized.length > MAX_HEADER_LENGTH && prepared.storage) {
            delete prepared.storage;
            emitClientInfoError(requestContext, {
              code: "header-size-limit",
              area: null,
              key: null,
              dropped: "storage",
              limit: MAX_HEADER_LENGTH
            });
            serialized = JSON.stringify(prepared);
          }
        }
        if (serialized.length > MAX_HEADER_LENGTH) {
          emitClientInfoError(requestContext, {
            code: "header-size-limit",
            area: null,
            key: null,
            dropped: "client-info",
            limit: MAX_HEADER_LENGTH
          });
          return null;
        }
        return serialized;
      } catch (error) {
        emitClientInfoError(requestContext, {
          code: "serialization-failed",
          area: null,
          key: null,
          errorName: error?.name || "SerializationError"
        });
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
  function createDomPipeline({ global, getConfig, boot, dbg, emitLifecycle, jsInvokeVoid, timeLocalization, mutations, plugins }) {
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
        origin: lifecycleContext.kind || "invocation",
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
      plugins?.stripCommands(fragment);
      if (timeLocalization && typeof timeLocalization.localize === "function") {
        const contextElement = mode === "outer" ? targetEl.parentElement : targetEl;
        timeLocalization.localize(fragment, {
          origin: detail.origin,
          kind: detail.kind,
          contextElement
        });
      }
      const nodes = fragmentToNodesArray(fragment);
      if (plugins) {
        if (mode === "outer") plugins.dispose(targetEl, nodes);
        else if (mode !== "beforeend" && mode !== "afterbegin") {
          for (const child of Array.from(targetEl.children)) plugins.dispose(child, nodes);
        }
      }
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
      for (const node of nodes) {
        if (node.nodeType === 1) plugins?.boot(node);
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
    function stripStorageFromFragment(fragment) {
      if (!fragment || !fragment.querySelectorAll)
        return;
      const directives = fragment.querySelectorAll("storage");
      for (const directive of directives)
        directive.remove();
    }
    function emitStorageError(sourceEl, context, detail) {
      if (!emitLifecycle)
        return;
      emitLifecycle(sourceEl, "heimdall:storage-error", {
        phase: "response-directive",
        sourceElement: sourceEl || null,
        ...context || {},
        ...detail
      });
    }
    function processStorageDirectives(fragment, sourceEl, context) {
      if (!fragment || !fragment.querySelectorAll)
        return 0;
      const directives = Array.from(fragment.querySelectorAll("storage"));
      let applied = 0;
      for (const directive of directives) {
        const area = String(getAttr(directive, "area") || "").trim().toLowerCase();
        const operation = String(getAttr(directive, "operation") || "").trim().toLowerCase();
        const key = getAttr(directive, "key");
        if (area !== "local" && area !== "session") {
          emitStorageError(sourceEl, context, {
            code: "invalid-area",
            area: area || null,
            operation: operation || null,
            key: key || null
          });
          directive.remove();
          continue;
        }
        if (operation !== "set" && operation !== "remove") {
          emitStorageError(sourceEl, context, {
            code: "invalid-operation",
            area,
            operation: operation || null,
            key: key || null
          });
          directive.remove();
          continue;
        }
        if (!key || !key.trim()) {
          emitStorageError(sourceEl, context, {
            code: "missing-key",
            area,
            operation,
            key: null
          });
          directive.remove();
          continue;
        }
        let storage;
        try {
          storage = global[area === "local" ? "localStorage" : "sessionStorage"];
        } catch (error) {
          emitStorageError(sourceEl, context, {
            code: "storage-area-unavailable",
            area,
            operation,
            key,
            errorName: error?.name || "StorageError"
          });
          directive.remove();
          continue;
        }
        try {
          if (!storage)
            throw new Error("Storage area is unavailable.");
          if (operation === "set")
            storage.setItem(key, directive.textContent || "");
          else
            storage.removeItem(key);
          applied++;
        } catch (error) {
          emitStorageError(sourceEl, context, {
            code: operation === "set" ? "storage-write-failed" : "storage-remove-failed",
            area,
            operation,
            key,
            errorName: error?.name || "StorageError"
          });
        }
        directive.remove();
      }
      return applied;
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
      const hasStorage = containsTag(html, "storage");
      const hasPluginCommand = containsTag(html, "heimdall-command");
      if (!hasScript && !hasInv && !hasAbort && !hasRedirect && !hasJsInvokeVoid && !hasMutation && !hasHistory && !hasStorage && !hasPluginCommand)
        return html;
      const tpl = parseHtmlToTemplate(html);
      stripInvocationsFromFragment(tpl.content);
      stripAbortsFromFragment(tpl.content);
      stripRedirectsFromFragment(tpl.content);
      stripJsInvokeVoidFromFragment(tpl.content);
      stripMutationsFromFragment(tpl.content);
      stripHistoryFromFragment(tpl.content);
      stripStorageFromFragment(tpl.content);
      plugins?.stripCommands(tpl.content);
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
      const hasStorage = containsTag(html, "storage");
      if (!hasInv && !hasScript && !hasAbort && !hasRedirect && !hasJsInvokeVoid && !hasMutation && !hasHistory && !hasStorage) {
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
        stripStorageFromFragment(fragment);
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
      processStorageDirectives(fragment, sourceEl, context);
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
          plugins?.stripCommands(payloadFrag);
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
      stripHistoryFromFragment,
      stripStorageFromFragment,
      processStorageDirectives
    };
  }

  // core/event-delegates.js
  function createEventDelegates({
    attributes,
    getConfig,
    runInvocationFromElement
  }) {
    const { get: getAttr2, int: intAttr, truthy: truthyAttr } = attributes;
    function parseTokenList(value) {
      return String(value || "").split(/\s+/).map((x) => x.trim().toLowerCase()).filter(Boolean);
    }
    function elementIgnoresTrigger(el, triggerName) {
      if (!el || !el.getAttribute)
        return false;
      const raw = getAttr2(el, "heimdall-ignore");
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
      const scope = (getAttr2(actionEl, "heimdall-scope") || "closest").toLowerCase().trim();
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
      const actionEl = target.closest(attributes.selector(triggerAttr));
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
      const actionId = getAttr2(el, "heimdall-content-click");
      if (!actionId)
        return;
      const isAnchor = el.tagName === "A";
      const preventDefault = truthyAttr(el, "heimdall-prevent-default", isAnchor);
      if (preventDefault)
        e.preventDefault();
      await runInvocationFromElement(el, actionId, "click");
    }
    async function handleChange(e) {
      if (e.defaultPrevented)
        return;
      const el = resolveActionElement(e.target, "heimdall-content-change", "change");
      if (!el)
        return;
      const actionId = getAttr2(el, "heimdall-content-change");
      if (!actionId)
        return;
      const ms = intAttr(el, "heimdall-debounce", 0);
      if (ms > 0) {
        scheduleDebounced(el, "change", ms, () => {
          if (!el.isConnected || getAttr2(el, "heimdall-content-change") !== actionId) return;
          runInvocationFromElement(el, actionId, "change").catch(() => {
          });
        });
        return;
      }
      await runInvocationFromElement(el, actionId, "change");
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
      return () => {
        clearTimeout(tid);
        if (map.get(key) === tid) map.delete(key);
      };
    }
    async function handleInput(e) {
      if (e.defaultPrevented)
        return;
      const el = resolveActionElement(e.target, "heimdall-content-input", "input");
      if (!el)
        return;
      const actionId = getAttr2(el, "heimdall-content-input");
      if (!actionId)
        return;
      const ms = intAttr(el, "heimdall-debounce", getConfig().inputDebounceMs || 250);
      if (ms > 0) {
        scheduleDebounced(el, "input", ms, () => {
          if (!el.isConnected || getAttr2(el, "heimdall-content-input") !== actionId) return;
          runInvocationFromElement(el, actionId, "input").catch(() => {
          });
        });
        return;
      }
      await runInvocationFromElement(el, actionId, "input");
    }
    async function handleSubmit(e) {
      if (e.defaultPrevented)
        return;
      const form = resolveActionElement(e.target, "heimdall-content-submit", "submit");
      if (!form)
        return;
      const actionId = getAttr2(form, "heimdall-content-submit");
      if (!actionId)
        return;
      const preventDefault = truthyAttr(form, "heimdall-prevent-default", true);
      if (preventDefault)
        e.preventDefault();
      await runInvocationFromElement(form, actionId, "submit");
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
      const actionId = getAttr2(el, "heimdall-content-keydown");
      if (!actionId)
        return;
      const keySpec = getAttr2(el, "heimdall-key");
      if (keySpec && !matchesKeySpec(e, keySpec))
        return;
      const wantsPreventDefault = truthyAttr(
        el,
        "heimdall-prevent-default",
        String(keySpec || "").toLowerCase() === "enter" && (e.target && (e.target.tagName === "INPUT" || e.target.tagName === "TEXTAREA"))
      );
      if (wantsPreventDefault)
        e.preventDefault();
      await runInvocationFromElement(el, actionId, "keydown");
    }
    async function handleFocusOut(e) {
      if (e.defaultPrevented)
        return;
      const el = resolveActionElement(e.target, "heimdall-content-blur", "blur");
      if (!el)
        return;
      const actionId = getAttr2(el, "heimdall-content-blur");
      if (!actionId)
        return;
      await runInvocationFromElement(el, actionId, "blur");
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
      const actionId = getAttr2(el, "heimdall-content-hover");
      if (!actionId)
        return;
      const delay = intAttr(el, "heimdall-hover-delay", getConfig().hoverDelayMs || 150);
      const prev = _hoverTimers.get(el);
      if (prev)
        clearTimeout(prev);
      if (delay > 0) {
        const tid = setTimeout(() => {
          _hoverTimers.delete(el);
          if (!el.isConnected || getAttr2(el, "heimdall-content-hover") !== actionId) return;
          runInvocationFromElement(el, actionId, "hover").catch(() => {
          });
        }, delay);
        _hoverTimers.set(el, tid);
        return;
      }
      await runInvocationFromElement(el, actionId, "hover");
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
      resolveTriggerElement: resolveActionElement,
      scheduleDebounced,
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
  function createMutationRuntime({ global, attributes, emitLifecycle, dbg, boot, busyState, plugins }) {
    function mutationError(sourceEl, directive, context, code, message, error) {
      const detail = {
        origin: context && context.kind ? context.kind : "invocation",
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
      return name === "lang" || attributes.recognizes(name);
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
        origin: context.kind || "invocation",
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
      const reconcileTargets = plugins?.hasPlugins() || operations.some(requiresBehaviorReconciliation) ? targets : [];
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
    attributes,
    runInvocationFromElement,
    emit
  }) {
    const getAttr2 = attributes.get;
    function invokeMatching(attr, triggerName) {
      const elements = document.querySelectorAll(attributes.selector(attr));
      for (const el of elements) {
        const actionId = (getAttr2(el, attr) || "").trim();
        if (!actionId)
          continue;
        runInvocationFromElement(el, actionId, triggerName).catch(() => {
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
  function createPayloadResolver(global, resolvePluginPayload, attributes) {
    const getAttr2 = attributes.get;
    function findClosestStateElement(el, key) {
      let cur = el;
      while (cur && cur.nodeType === 1) {
        if (key) {
          const attr = `data-heimdall-state-${key}`;
          if (cur.hasAttribute && attributes.has(cur, attr))
            return cur;
        } else {
          if (cur.hasAttribute && attributes.has(cur, "data-heimdall-state"))
            return cur;
        }
        cur = cur.parentElement;
      }
      return null;
    }
    function readStateAttribute(host, attr) {
      if (!host || !host.hasAttribute || !attributes.has(host, attr))
        return null;
      const raw = getAttr2(host, attr);
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
          if (host.isConnected === false || !attributes.has(host, attribute)) {
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
      const ref = getAttr2(el, "heimdall-payload-ref");
      if (ref)
        return getByPath(global, ref);
      const from = (getAttr2(el, "heimdall-payload-from") || "").trim();
      if (from.toLowerCase().startsWith("ref:")) {
        const path = from.substring(4).trim();
        return getByPath(global, path);
      }
      return void 0;
    }
    function readStoragePayload(area, key) {
      let storage;
      try {
        storage = global[area === "local" ? "localStorage" : "sessionStorage"];
      } catch {
        return null;
      }
      if (!storage || typeof storage.getItem !== "function")
        return null;
      try {
        const raw = storage.getItem(key);
        return raw == null ? null : safeJsonParse(raw);
      } catch {
        return null;
      }
    }
    function payloadBindingFromElement(el) {
      const payloadAttr = getAttr2(el, "heimdall-payload");
      if (payloadAttr)
        return { value: safeJsonParse(payloadAttr), binding: null };
      const refObj = resolvePayloadRef(el);
      if (refObj !== void 0)
        return { value: refObj, binding: null };
      const fromRaw = (getAttr2(el, "heimdall-payload-from") || "").trim();
      const from = fromRaw.toLowerCase();
      if (from.startsWith("plugin:")) {
        if (!resolvePluginPayload) throw new Error("Plugin payload sources are unavailable.");
        return resolvePluginPayload(fromRaw.substring("plugin:".length).trim(), el);
      }
      if (from === "closest-state" || from.startsWith("closest-state:")) {
        const key = from.startsWith("closest-state:") ? fromRaw.substring("closest-state:".length).trim() : null;
        const binding = createClosestStateBinding(el, key || null);
        return binding ? { value: binding.value, binding } : { value: null, binding: null };
      }
      if (from.startsWith("local-storage:") || from.startsWith("session-storage:")) {
        const separator = from.indexOf(":");
        const area = from.substring(0, separator) === "local-storage" ? "local" : "session";
        const key = fromRaw.substring(separator + 1).trim();
        return key ? { value: readStoragePayload(area, key), binding: null } : { value: null, binding: null };
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

  // core/plugins.js
  var PLUGIN_API_VERSION = 1;
  var validName = /^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$/;
  function abortError() {
    return new DOMException("Plugin operation was cancelled.", "AbortError");
  }
  function waitFor(promise, signal, timeoutMs) {
    return new Promise((resolve, reject) => {
      let timer;
      const finish = (callback, value) => {
        clearTimeout(timer);
        signal?.removeEventListener("abort", abort);
        callback(value);
      };
      const abort = () => finish(reject, abortError());
      Promise.resolve(promise).then(
        (value) => finish(resolve, value),
        (error) => finish(reject, error)
      );
      if (signal?.aborted) {
        reject(abortError());
        return;
      }
      signal?.addEventListener("abort", abort, { once: true });
      if (timeoutMs > 0) {
        timer = setTimeout(() => {
          const error = new Error("Plugin operation timed out.");
          error.code = "plugin-timeout";
          finish(reject, error);
        }, timeoutMs);
      }
    });
  }
  function matchingElements(root, selector) {
    const elements = root?.matches?.(selector) ? [root] : [];
    return elements.concat(Array.from(root?.querySelectorAll?.(selector) || []));
  }
  function snapshot(value, global) {
    if (typeof global.FormData === "function" && value instanceof global.FormData) {
      const copy = new global.FormData();
      for (const [key, item] of value.entries()) copy.append(key, item);
      return copy;
    }
    return value == null ? value : JSON.parse(JSON.stringify(value));
  }
  function createPluginRuntime({ global, attributes, getConfig, emitLifecycle, dbg }) {
    const { int: intAttr, truthy: truthyAttr } = attributes;
    const installed = /* @__PURE__ */ new Map();
    const registries = {
      behaviors: /* @__PURE__ */ new Map(),
      triggers: /* @__PURE__ */ new Map(),
      payloads: /* @__PURE__ */ new Map(),
      commands: /* @__PURE__ */ new Map()
    };
    let host = {};
    let started = false;
    let registryChanged = () => {
    };
    function report(pluginId, extension, phase, code, error, extra = {}) {
      const detail = { pluginId, extension, phase, code, error, ...extra };
      emitLifecycle(extra.sourceElement || null, "heimdall:plugin-error", detail);
      dbg("plugin error", detail);
      return error;
    }
    function checkName(value, description) {
      if (typeof value !== "string" || value.length > 128 || !validName.test(value))
        throw new TypeError(`${description} must be a lowercase dotted or hyphenated name.`);
      return value;
    }
    function disposeRecord(definition, element, record) {
      definition.instances.delete(element);
      record.controller.abort();
      try {
        const result = record.dispose?.();
        if (result && typeof result.then === "function")
          Promise.resolve(result).catch((error) => report(
            definition.owner.id,
            definition.name,
            "dispose",
            "dispose-failed",
            error,
            { sourceElement: element }
          ));
      } catch (error) {
        report(
          definition.owner.id,
          definition.name,
          "dispose",
          "dispose-failed",
          error,
          { sourceElement: element }
        );
      }
    }
    function invokeCleanup(owner, callback) {
      try {
        const result = callback();
        if (result && typeof result.then === "function")
          Promise.resolve(result).catch((error) => report(owner.id, null, "dispose", "dispose-failed", error));
      } catch (error) {
        report(owner.id, null, "dispose", "dispose-failed", error);
      }
    }
    function remove(id) {
      const owner = installed.get(id);
      if (!owner) return false;
      for (const other of installed.values()) {
        if (other.requires.includes(id))
          throw new Error(`Plugin '${id}' is required by '${other.id}'.`);
      }
      attributes.transaction(() => {
        installed.delete(id);
        owner.controller.abort();
        for (const definition of owner.definitions) {
          registries[definition.kind].delete(definition.name);
          for (const [element, record] of definition.instances || [])
            disposeRecord(definition, element, record);
        }
        for (const callback of owner.cleanup.reverse()) invokeCleanup(owner, callback);
        attributes.removeOwner(owner);
      });
      registryChanged();
      return true;
    }
    function use(plugin, options) {
      if (!plugin || typeof plugin !== "object" || typeof plugin.install !== "function")
        throw new TypeError("A plugin must provide an install function.");
      const id = checkName(plugin.id, "Plugin id");
      if (id === "heimdall" || id.startsWith("heimdall."))
        throw new Error("The heimdall namespace is reserved.");
      if (plugin.pluginApiVersion !== PLUGIN_API_VERSION)
        throw new Error(`Plugin '${id}' requires an unsupported plugin API version.`);
      if (plugin.version != null && (typeof plugin.version !== "string" || !plugin.version.trim()))
        throw new TypeError("Plugin version must be a non-empty string when provided.");
      const existing = installed.get(id);
      if (existing) {
        if (existing.plugin === plugin && existing.options === options) return;
        throw new Error(`Plugin '${id}' is already installed.`);
      }
      const requires = plugin.requires || [];
      if (!Array.isArray(requires)) throw new TypeError("Plugin requires must be an array of plugin ids.");
      for (const dependency of requires) {
        checkName(dependency, "Dependency id");
        if (!installed.has(dependency))
          throw new Error(`Plugin '${id}' requires '${dependency}' to be installed first.`);
      }
      const owner = {
        id,
        plugin,
        options,
        version: plugin.version || null,
        requires: [...requires],
        controller: new AbortController(),
        definitions: [],
        cleanup: [],
        attributes: /* @__PURE__ */ new Set()
      };
      let installing = true;
      const assertInstalling = () => {
        if (!installing) throw new Error("Plugin registrations must be made synchronously during install.");
      };
      function register(kind, descriptor) {
        assertInstalling();
        if (!descriptor || typeof descriptor !== "object")
          throw new TypeError("An extension descriptor is required.");
        const localName = checkName(descriptor.name, "Extension name");
        const name = `${id}.${localName}`;
        if (owner.definitions.some((d) => d.kind === kind && d.name === name))
          throw new Error(`Extension '${name}' is already registered.`);
        const definition = { ...descriptor, name, kind, owner, instances: /* @__PURE__ */ new Map() };
        if (kind === "behaviors") {
          global.document.querySelectorAll(descriptor.selector);
          if (!descriptor.selector || typeof descriptor.mount !== "function")
            throw new TypeError("A behavior requires a selector and mount function.");
          if (descriptor.observedAttributes != null && (!Array.isArray(descriptor.observedAttributes) || descriptor.observedAttributes.some((a) => typeof a !== "string" || !a)))
            throw new TypeError("observedAttributes must contain attribute names.");
          definition.observedAttributes = [...descriptor.observedAttributes || []];
        } else if (kind === "triggers") {
          const attribute = descriptor.attribute || `heimdall-content-${name.replace(/\./g, "-")}`;
          const events = descriptor.events || (descriptor.event ? [descriptor.event] : []);
          if (!Array.isArray(events) || events.some((e) => typeof e !== "string" || !e || /\s/.test(e)))
            throw new TypeError("Trigger events must be an array of individual DOM event names.");
          if (events.length > 0 === (typeof descriptor.subscribe === "function"))
            throw new TypeError("A trigger requires either DOM events or a subscribe function.");
          definition.attribute = attribute;
          if (descriptor.filter != null && typeof descriptor.filter !== "function")
            throw new TypeError("A trigger filter must be a function.");
          definition.events = [...events];
          if (descriptor.aliases != null && !Array.isArray(descriptor.aliases))
            throw new TypeError("Trigger aliases must be an array of attribute names.");
          attributes.register(attribute, owner, descriptor.aliases || []);
        } else if (kind === "payloads") {
          if (typeof descriptor.read !== "function" || !["trigger", "execution"].includes(descriptor.timing || "trigger"))
            throw new TypeError("A payload source requires read and trigger/execution timing.");
        } else if (typeof descriptor.handle !== "function") {
          throw new TypeError("A command requires a handle function.");
        }
        if (kind === "commands" && descriptor.validate != null && typeof descriptor.validate !== "function")
          throw new TypeError("A command validator must be a function.");
        owner.definitions.push(definition);
        return name;
      }
      const api = {
        pluginId: id,
        pluginApiVersion: PLUGIN_API_VERSION,
        signal: owner.controller.signal,
        attributes: attributes.scoped(owner, assertInstalling),
        invoke: (invocationId, payload, options2 = {}) => {
          if (owner.controller.signal.aborted) return Promise.reject(abortError());
          if (installed.get(id) !== owner)
            throw new Error("Plugin invocation is available after installation commits.");
          const controller = new AbortController();
          const signals = [owner.controller.signal, options2.signal].filter(Boolean);
          const abort = () => controller.abort();
          const unlink = () => {
            for (const signal of signals) signal.removeEventListener("abort", abort);
          };
          for (const signal of signals) {
            if (signal.aborted) abort();
            else signal.addEventListener("abort", abort, { once: true });
          }
          try {
            return Promise.resolve(host.invoke(invocationId, payload, { ...options2, signal: controller.signal })).finally(unlink);
          } catch (error) {
            unlink();
            throw error;
          }
        },
        onDispose(callback) {
          assertInstalling();
          if (typeof callback !== "function") throw new TypeError("A cleanup function is required.");
          owner.cleanup.push(callback);
        }
      };
      for (const kind of Object.keys(registries))
        api[kind] = Object.freeze({ register: (descriptor) => register(kind, descriptor) });
      try {
        attributes.transaction(() => {
          const result = plugin.install(Object.freeze(api), options);
          if (result && typeof result.then === "function") {
            Promise.resolve(result).catch(() => {
            });
            throw new TypeError("Plugin install must be synchronous. Async work belongs in element behaviors.");
          }
          if (result !== void 0 && typeof result !== "function")
            throw new TypeError("Plugin install may return a cleanup function or undefined.");
          if (result) owner.cleanup.push(result);
          installing = false;
          installed.set(id, owner);
          for (const definition of owner.definitions) registries[definition.kind].set(definition.name, definition);
        });
      } catch (error) {
        owner.controller.abort();
        for (const callback of owner.cleanup.reverse()) invokeCleanup(owner, callback);
        report(id, null, "install", "install-failed", error);
        throw error;
      } finally {
        installing = false;
      }
      registryChanged();
      if (started) host.boot(global.document);
    }
    function signature(definition, element) {
      return JSON.stringify((definition.observedAttributes || []).map((a) => attributes.get(element, a)));
    }
    function mountBehavior(definition, element) {
      const previous = definition.instances.get(element);
      const nextSignature = signature(definition, element);
      if (previous) {
        if (previous.signature === nextSignature || previous.pending) return;
        if (previous.update) {
          previous.signature = nextSignature;
          try {
            const result = previous.update({ element, signal: previous.controller.signal });
            if (result && typeof result.then === "function") {
              Promise.resolve(result).catch(() => {
              });
              throw new TypeError("Behavior update must be synchronous; remount for asynchronous updates.");
            }
          } catch (error) {
            report(
              definition.owner.id,
              definition.name,
              "update",
              "update-failed",
              error,
              { sourceElement: element }
            );
          }
          return;
        }
        disposeRecord(definition, element, previous);
      }
      const record = { controller: new AbortController(), signature: nextSignature, pending: false, ready: null };
      definition.instances.set(element, record);
      function accept(result) {
        if (result != null && typeof result !== "function" && typeof result !== "object")
          throw new TypeError("Behavior mount must return cleanup, a behavior instance, or undefined.");
        record.dispose = typeof result === "function" ? result : result?.dispose?.bind(result);
        record.update = typeof result === "object" ? result?.update?.bind(result) : null;
        if (record.controller.signal.aborted) return record.dispose?.();
        record.pending = false;
        if (!record.controller.signal.aborted) mountBehavior(definition, element);
      }
      try {
        const result = definition.mount(element, {
          element,
          pluginId: definition.owner.id,
          options: definition.owner.options,
          signal: record.controller.signal
        });
        if (result && typeof result.then === "function") {
          record.pending = true;
          const mounting = Promise.resolve(result).then(accept);
          record.ready = waitFor(
            mounting,
            record.controller.signal,
            getConfig().pluginMountTimeoutMs ?? 1e4
          ).then(() => {
            record.ready = null;
          }).catch((error) => {
            record.pending = false;
            record.controller.abort();
            if (error?.name !== "AbortError")
              report(
                definition.owner.id,
                definition.name,
                "mount",
                error?.code || "mount-failed",
                error,
                { sourceElement: element }
              );
            throw error;
          });
          record.ready.catch(() => {
          });
        } else accept(result);
      } catch (error) {
        record.controller.abort();
        record.ready = Promise.reject(error);
        record.ready.catch(() => {
        });
        report(
          definition.owner.id,
          definition.name,
          "mount",
          "mount-failed",
          error,
          { sourceElement: element }
        );
      }
    }
    function mountTrigger(definition, element) {
      if (definition.instances.has(element)) return;
      const record = { controller: new AbortController() };
      definition.instances.set(element, record);
      const emit = async (event = null, overrides = {}) => {
        if (record.controller.signal.aborted || !element.isConnected) return Promise.resolve(null);
        if (event?.defaultPrevented) return Promise.resolve(null);
        const eventTarget = event?.target || element;
        if (host.resolveTriggerElement(eventTarget, definition.attribute, definition.name) !== element)
          return Promise.resolve(null);
        if (definition.filter && !definition.filter({ element, event })) return Promise.resolve(null);
        const submitted = { ...overrides };
        if (Object.prototype.hasOwnProperty.call(submitted, "payload"))
          submitted.payload = snapshot(submitted.payload, global);
        if (truthyAttr(element, "heimdall-prevent-default", !!definition.preventDefault)) event?.preventDefault?.();
        const invoke = () => {
          if (record.controller.signal.aborted || !element.isConnected) return;
          return host.runInvocationFromElement(
            element,
            attributes.get(element, definition.attribute),
            definition.name,
            { ...submitted, event }
          );
        };
        const delay = intAttr(element, "heimdall-debounce", Number(definition.debounceMs || 0));
        if (delay > 0) {
          record.cancelDebounce?.();
          return new Promise((resolve, reject) => {
            const cancel = host.scheduleDebounced(element, definition.name, delay, () => {
              record.cancelDebounce = null;
              Promise.resolve(invoke()).then(resolve, reject);
            });
            record.cancelDebounce = () => {
              cancel();
              resolve(null);
            };
          });
        }
        return Promise.resolve(invoke());
      };
      try {
        for (const event of definition.events) {
          element.addEventListener(event, (e) => {
            emit(e).catch((error) => report(
              definition.owner.id,
              definition.name,
              "trigger",
              "trigger-failed",
              error,
              { sourceElement: element }
            ));
          }, { signal: record.controller.signal });
        }
        const cleanup = definition.subscribe?.({ element, emit, signal: record.controller.signal });
        if (cleanup && typeof cleanup.then === "function") Promise.resolve(cleanup).catch(() => {
        });
        if (cleanup != null && typeof cleanup !== "function")
          throw new TypeError("Trigger subscribe must return cleanup or undefined.");
        record.dispose = () => {
          record.cancelDebounce?.();
          cleanup?.();
        };
      } catch (error) {
        record.controller.abort();
        record.cancelDebounce?.();
        report(
          definition.owner.id,
          definition.name,
          "subscribe",
          "subscribe-failed",
          error,
          { sourceElement: element }
        );
      }
    }
    function boot(root) {
      if (!root || root.nodeType === 1 && !root.isConnected) return;
      for (const kind of ["behaviors", "triggers"]) {
        for (const definition of registries[kind].values()) {
          const selector = kind === "behaviors" ? definition.selector : attributes.selector(definition.attribute);
          for (const [element, record] of definition.instances) {
            if (!element.isConnected || !element.matches(selector))
              disposeRecord(definition, element, record);
          }
          for (const element of matchingElements(root, selector)) {
            if (kind === "behaviors") mountBehavior(definition, element);
            else mountTrigger(definition, element);
          }
        }
      }
    }
    function dispose(root, preserved = []) {
      for (const kind of ["triggers", "behaviors"]) {
        for (const definition of registries[kind].values()) {
          for (const [element, record] of definition.instances) {
            if ((root === element || root?.contains?.(element)) && !preserved.some((node) => node === element || node.contains?.(element)))
              disposeRecord(definition, element, record);
          }
        }
      }
    }
    function ready(root, subtree = false) {
      const promises = [];
      for (const definition of registries.behaviors.values()) {
        for (const [element, record] of definition.instances) {
          if (record.ready && (element === root || element.contains(root) || subtree && root?.contains?.(element))) {
            promises.push(record.ready.catch((error) => {
              const replacement = definition.instances.get(element);
              if (error?.name === "AbortError" && replacement && replacement !== record) return;
              throw error;
            }));
          }
        }
      }
      return promises.length ? Promise.all(promises).then(() => ready(root, subtree)) : null;
    }
    function readPayload(name, element) {
      const definition = registries.payloads.get(name);
      if (!definition) throw report(null, name, "payload", "unknown-payload", new Error(`Unknown plugin payload '${name}'.`), { sourceElement: element });
      const read = () => {
        try {
          const value = definition.read({ element, options: definition.owner.options });
          if (value && typeof value.then === "function") {
            Promise.resolve(value).catch(() => {
            });
            throw new TypeError("Payload readers must be synchronous. Initialize asynchronous data in a behavior.");
          }
          return snapshot(value, global);
        } catch (error) {
          throw report(definition.owner.id, name, "payload", "payload-failed", error, { sourceElement: element });
        }
      };
      return {
        value: read(),
        binding: definition.timing === "execution" ? {
          kind: "plugin",
          resolve: () => element.isConnected && registries.payloads.get(name) === definition ? { available: true, value: read() } : { available: false, value: null }
        } : null
      };
    }
    function prepareCommands(html) {
      if (!/<heimdall-command[\s>]/i.test(html || "")) return { html, before: [], after: [] };
      const template = global.document.createElement("template");
      template.innerHTML = html;
      const redirect = template.content.querySelector("redirect");
      const suppress = !!(redirect && (redirect.getAttribute("url")?.trim() || redirect.textContent.trim()));
      const before = [], after = [];
      let limitReported = false;
      for (const node of Array.from(template.content.querySelectorAll("heimdall-command"))) {
        if (before.length + after.length >= (getConfig().pluginCommandMaxCount ?? 256)) {
          if (!limitReported) report(null, null, "command", "command-limit", new Error("Too many plugin commands in a response."));
          limitReported = true;
        } else if (!suppress) {
          const command = {
            name: node.getAttribute("name"),
            argsJson: node.getAttribute("args") || "{}",
            target: node.getAttribute("target"),
            all: node.getAttribute("all") === "true",
            timing: node.getAttribute("timing") || "after"
          };
          (command.timing === "before" ? before : after).push(command);
        }
        node.remove();
      }
      stripCommands(template.content);
      const wrapper = global.document.createElement("div");
      wrapper.append(template.content);
      return { html: wrapper.innerHTML, before, after };
    }
    function stripCommands(root) {
      for (const node of Array.from(root?.querySelectorAll?.("heimdall-command") || [])) node.remove();
      for (const template of Array.from(root?.querySelectorAll?.("template") || [])) stripCommands(template.content);
    }
    async function runCommands(commands, context) {
      for (const command of commands) {
        if (context.isCurrent && !context.isCurrent()) return false;
        const definition = registries.commands.get(command.name);
        if (!definition) {
          report(null, command.name, "command", "unknown-command", new Error(`Unknown plugin command '${command.name}'.`), context);
          continue;
        }
        const controller = new AbortController();
        const signals = [context.signal, definition.owner.controller.signal].filter(Boolean);
        const abort = () => controller.abort();
        for (const signal of signals) {
          if (signal.aborted) abort();
          else signal.addEventListener("abort", abort, { once: true });
        }
        try {
          if (!["before", "after"].includes(command.timing)) throw new Error("Invalid command timing.");
          if (command.argsJson.length > (getConfig().pluginCommandMaxArgsChars ?? 65536))
            throw new Error("Plugin command arguments exceed the configured limit.");
          const args = JSON.parse(command.argsJson);
          if (definition.validate && definition.validate(args) !== true) throw new Error("Invalid plugin command arguments.");
          const targets = command.target ? command.all ? Array.from(global.document.querySelectorAll(command.target)) : [global.document.querySelector(command.target)].filter(Boolean) : [null];
          if (!targets.length) throw new Error(`Plugin command target '${command.target}' was not found.`);
          for (const element of targets) {
            if (controller.signal.aborted || context.isCurrent && !context.isCurrent()) return false;
            if (element) {
              boot(element);
              const pending = ready(element, true);
              if (pending) await waitFor(pending, controller.signal, getConfig().pluginMountTimeoutMs ?? 1e4);
            }
            if (controller.signal.aborted || context.isCurrent && !context.isCurrent()) return false;
            if (element && !element.isConnected) throw new Error("Plugin command target was disconnected.");
            await waitFor(definition.handle({
              element,
              args,
              signal: controller.signal,
              pluginId: definition.owner.id,
              invocationId: context.invocationId || null,
              sourceElement: context.sourceElement || null,
              origin: context.origin || "invocation",
              timing: command.timing
            }), controller.signal, getConfig().pluginCommandTimeoutMs ?? 1e4);
          }
        } catch (error) {
          if (error?.name !== "AbortError")
            report(
              definition.owner.id,
              command.name,
              "command",
              error?.code || "command-failed",
              error,
              { invocationId: context.invocationId || null, sourceElement: context.sourceElement || null }
            );
        } finally {
          controller.abort();
          for (const signal of signals) signal.removeEventListener("abort", abort);
        }
      }
      return !context.isCurrent || context.isCurrent();
    }
    return {
      pluginApiVersion: PLUGIN_API_VERSION,
      use,
      remove,
      boot,
      dispose,
      ready,
      readPayload,
      prepareCommands,
      stripCommands,
      runCommands,
      configure(value) {
        host = value;
      },
      start() {
        started = true;
      },
      hasPlugins: () => installed.size > 0,
      onRegistryChanged(callback) {
        registryChanged = callback;
      },
      observe(mutations) {
        if (!installed.size) return;
        const roots = new Set(mutations.map((m) => m.target).filter((n) => n.isConnected));
        for (const root of roots) {
          if (!Array.from(roots).some((other) => other !== root && other.contains?.(root))) boot(root);
        }
      },
      list: () => Array.from(installed.values(), (owner) => Object.freeze({
        id: owner.id,
        version: owner.version,
        pluginApiVersion: PLUGIN_API_VERSION,
        requires: Object.freeze([...owner.requires])
      }))
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
    attributes,
    reconcileAttributes,
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
    plugins,
    sseConnect,
    sseDisconnect,
    sseDisconnectAll,
    handlers,
    installPageLifecycle,
    installSseSweeper,
    dbg,
    onRuntimeCreated
  }) {
    let started = false;
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
      const obs = new MutationObserver((mutations) => {
        plugins.observe(mutations);
        for (const m of mutations) {
          if (m.type === "attributes") {
            if (m.attributeName !== "lang" && !attributes.recognizes(m.attributeName)) continue;
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
      function observe() {
        if (!Heimdall.config.observeDom) return;
        obs.observe(document.documentElement, {
          childList: true,
          subtree: true,
          attributes: true
        });
      }
      observe();
      plugins.onRegistryChanged(observe);
      Heimdall._observer = obs;
      dbg("MutationObserver installed");
    }
    const Heimdall = {
      apiVersion,
      invoke,
      boot,
      onReady: onReady2,
      clearCsrfToken,
      attributes: attributes.publicApi,
      pluginApiVersion: plugins.pluginApiVersion,
      use(plugin, options) {
        plugins.use(plugin, options);
        return Heimdall;
      },
      plugins: Object.freeze({ list: plugins.list, remove: plugins.remove }),
      dispose: plugins.dispose,
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
          contentInvocations: defaultContentEndpoint,
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
        clientInfoLimits: {
          maxKeysPerArea: 16,
          maxKeyLength: 128,
          maxValueLength: 1024,
          maxStorageChars: 2048,
          maxExtensionEntries: 16,
          maxExtensionChars: 1024,
          maxExtensionDepth: 4
        },
        inputDebounceMs: 250,
        hoverDelayMs: 150,
        scrollThresholdPx: 120,
        scrollMinIntervalMs: 250,
        requestSync: "parallel",
        requestTimeoutMs: 0,
        pluginMountTimeoutMs: 1e4,
        pluginCommandTimeoutMs: 1e4,
        pluginCommandMaxArgsChars: 65536,
        pluginCommandMaxCount: 256,
        pluginSseQueueLimit: 128,
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
    Object.defineProperty(Heimdall.config.endpoints, "contentActions", {
      configurable: true,
      enumerable: true,
      get() {
        return Heimdall.config.endpoints.contentInvocations;
      },
      set(value) {
        Heimdall.config.endpoints.contentInvocations = value;
      }
    });
    if (typeof onRuntimeCreated === "function")
      onRuntimeCreated(Heimdall);
    global.Heimdall = Heimdall;
    attributes.onChange(() => {
      if (!started) return;
      reconcileAttributes();
    });
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
      plugins.start();
      started = true;
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
  var SERVER_DISCONNECT_EVENT = "heimdall:disconnect";
  function createSseRuntime({
    global,
    attributes,
    getConfig,
    emit,
    dbg,
    dom,
    boot,
    clearBifrostSubscribeToken,
    ensureBifrostSubscribeToken,
    matchesTriggerAttr,
    plugins,
    defaultBifrostEndpoint
  }) {
    const { get: getAttr2, truthy: truthyAttr } = attributes;
    const _sseByElement = /* @__PURE__ */ new WeakMap();
    const _sseStates = /* @__PURE__ */ new Set();
    const _sseConnections = /* @__PURE__ */ new Map();
    function getSseTopic(el) {
      const t1 = getAttr2(el, "heimdall-sse");
      if (t1 && t1.trim())
        return t1.trim();
      const t2 = getAttr2(el, "heimdall-sse-topic");
      if (t2 && t2.trim())
        return t2.trim();
      return null;
    }
    function readSseConfig(el, options) {
      options = options || {};
      const config = getConfig();
      const topic = options.topic != null ? String(options.topic || "").trim() : getSseTopic(el);
      const eventName = (options.event != null ? String(options.event || "") : getAttr2(el, "heimdall-sse-event") || config.sseEventName || "heimdall").trim();
      const target = options.target != null ? options.target : getAttr2(el, "heimdall-sse-target") || el;
      const swap = String(options.swap != null ? options.swap : getAttr2(el, "heimdall-sse-swap") || config.sseDefaultSwap || "none").toLowerCase();
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
      state.commandController?.abort();
      state.commandEpoch = (state.commandEpoch || 0) + 1;
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
    function handleServerDisconnect(connection, ev) {
      if (!connection || connection.closed)
        return;
      const reason = ev && ev.data != null ? String(ev.data).trim() : "";
      closeSseConnectionSubscribers(connection, reason || "server-disconnected");
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
      return status === 400 || status === 401 || status === 403 || status === 404;
    }
    function tokenFailureReason(error) {
      const status = error && Number(error.status);
      if (status === 400)
        return "token-rejected";
      if (status === 401)
        return "auth-required";
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
        state.commandController?.abort();
        state.commandController = null;
        state.commandEpoch = (state.commandEpoch || 0) + 1;
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
        es.addEventListener(SERVER_DISCONNECT_EVENT, (ev) => {
          handleServerDisconnect(connection, ev);
        });
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
      if (eventName === SERVER_DISCONNECT_EVENT)
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
        const epoch = state.commandEpoch || 0;
        const deliver = () => {
          if ((state.commandEpoch || 0) !== epoch) return;
          return handleSsePayload(state, ev, rawData);
        };
        if (state.pluginQueue || /<heimdall-command[\s>]/i.test(String(rawData || ""))) {
          const queueLimit = Math.max(1, Number(getConfig().pluginSseQueueLimit) || 128);
          if ((state.pluginQueueDepth || 0) >= queueLimit) {
            emit("heimdall:plugin-error", {
              pluginId: null,
              extension: null,
              phase: "command",
              code: "sse-command-queue-full",
              sourceElement: state.el,
              error: new Error("The Bifrost plugin command queue is full.")
            });
            closeSseState(state, "plugin-command-queue-full");
            continue;
          }
          state.pluginQueueDepth = (state.pluginQueueDepth || 0) + 1;
          const queued = (state.pluginQueue || Promise.resolve()).then(deliver).catch((error) => {
            emit("heimdall:sse-error", { topic: state.topic, el: state.el, error });
          });
          state.pluginQueue = queued;
          queued.finally(() => {
            state.pluginQueueDepth--;
            if (state.pluginQueue === queued) state.pluginQueue = null;
          });
        } else {
          deliver()?.catch((error) => emit("heimdall:sse-error", { topic: state.topic, el: state.el, error }));
        }
      }
    }
    async function handleSsePayload(state, ev, rawData) {
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
      let pluginCommands = { before: [], after: [] };
      if (!state.commandController || state.commandController.signal.aborted)
        state.commandController = new AbortController();
      const commandEpoch = state.commandEpoch || 0;
      const pluginContext = {
        sourceElement: state.el,
        origin: "sse",
        signal: state.commandController.signal,
        isCurrent: () => !state.closed && !state.paused && state.el.isConnected && (state.commandEpoch || 0) === commandEpoch
      };
      try {
        pluginCommands = plugins.prepareCommands(html);
        html = pluginCommands.html;
        if (pluginCommands.before.length)
          await plugins.runCommands(pluginCommands.before, pluginContext);
        if (!pluginContext.isCurrent()) return;
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
      if (pluginCommands.after.length) {
        await plugins.runCommands(pluginCommands.after, pluginContext);
        if (!pluginContext.isCurrent()) return;
      }
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
      for (const el of scope.querySelectorAll(`${attributes.selector("heimdall-sse")},${attributes.selector("heimdall-sse-topic")}`))
        attachSse(el);
    }
    function reconcileAttributes(root) {
      for (const state of Array.from(_sseStates)) {
        if (!state.programmatic) attachSse(state.el);
      }
      bootSse(root);
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
      reconcileAttributes,
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
  function createTimeLocalization({ global, attributes, emitLifecycle, dbg }) {
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
      if (isElement(root) && attributes.has(root, TIME_ATTRIBUTE))
        candidates.push(root);
      if (root && typeof root.querySelectorAll === "function") {
        for (const element of root.querySelectorAll(attributes.selector(TIME_ATTRIBUTE)))
          candidates.push(element);
      }
      return candidates;
    }
    function localizeElement(element, options) {
      const rawValue = attributes.get(element, TIME_ATTRIBUTE);
      const rawFormat = attributes.get(element, FORMAT_ATTRIBUTE) || DEFAULT_FORMAT;
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
    const DEFAULT_CONTENT_ENDPOINT = `${DEFAULT_BASE_PATH}/v${API_VERSION}/content/invocations`;
    const DEFAULT_CSRF_ENDPOINT = `${DEFAULT_BASE_PATH}/v${API_VERSION}/csrf`;
    const DEFAULT_BIFROST_ENDPOINT = `${DEFAULT_BASE_PATH}/v${API_VERSION}/bifrost`;
    const DEFAULT_BIFROST_TOKEN_ENDPOINT = `${DEFAULT_BASE_PATH}/v${API_VERSION}/bifrost/token`;
    const INVOCATION_HEADER = "X-Heimdall-Content-Invocation";
    const CSRF_HEADER = "RequestVerificationToken";
    const CLIENT_INFO_HEADER = "X-Heimdall-Client-Info";
    const runtimeRef = { current: null };
    const getRuntimeConfig = () => runtimeRef.current && runtimeRef.current.config;
    const { emit, emitLifecycle, dbg } = createDiagnostics(getRuntimeConfig);
    const attributes = createAttributeRegistry();
    const plugins = createPluginRuntime({ global, attributes, getConfig: getRuntimeConfig, emitLifecycle, dbg });
    const { payloadBindingFromElement } = createPayloadResolver(global, plugins.readPayload, attributes);
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
      attributes,
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
      attributes,
      emitLifecycle,
      dbg,
      boot: (root) => boot(root),
      busyState,
      plugins
    });
    const dom = createDomPipeline({
      global,
      getConfig: getRuntimeConfig,
      boot: (root) => boot(root),
      dbg,
      emitLifecycle,
      jsInvokeVoid,
      timeLocalization,
      mutations,
      plugins
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
      runInvocationFromElement
    } = createInvocationInvoker({
      global,
      attributes,
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
      plugins,
      resolveRequestHeaders: requestHeaders.resolve,
      mergeRequestHeaders: requestHeaders.merge,
      getClientInfoHeader: clientInfo.getHeaderValue,
      invocationHeader: INVOCATION_HEADER,
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
      handleSubmit,
      resolveTriggerElement,
      scheduleDebounced
    } = createEventDelegates({
      attributes,
      getConfig: getRuntimeConfig,
      runInvocationFromElement
    });
    const {
      bootLoads,
      bootPoll,
      bootScroll,
      bootVisible,
      reconcileAttributes: reconcileBootAttributes,
      matchesTriggerAttr
    } = createBootTriggers({
      global,
      attributes,
      getConfig: getRuntimeConfig,
      runInvocationFromElement
    });
    const pageLifecycle = createPageLifecycleRuntime({
      global,
      attributes,
      runInvocationFromElement,
      emit
    });
    const {
      bootSse,
      reconcileAttributes: reconcileSseAttributes,
      installSseSweeper,
      sseConnect,
      sseDisconnect,
      sseDisconnectAll
    } = createSseRuntime({
      global,
      attributes,
      getConfig: getRuntimeConfig,
      emit,
      dbg,
      dom,
      boot: (root) => boot(root),
      clearBifrostSubscribeToken,
      ensureBifrostSubscribeToken,
      matchesTriggerAttr,
      plugins,
      defaultBifrostEndpoint: DEFAULT_BIFROST_ENDPOINT
    });
    function boot(root) {
      timeLocalization.localize(root, { origin: "boot" });
      plugins.boot(root);
      bootLoads(root);
      bootVisible(root);
      bootScroll(root);
      bootPoll(root);
      bootSse(root);
    }
    plugins.configure({ invoke, runInvocationFromElement, resolveTriggerElement, scheduleDebounced, boot });
    const Heimdall = createHeimdallRuntime({
      global,
      attributes,
      reconcileAttributes() {
        timeLocalization.localize(document, { origin: "boot" });
        plugins.boot(document);
        reconcileBootAttributes(document);
        reconcileSseAttributes(document);
      },
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
      plugins,
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
