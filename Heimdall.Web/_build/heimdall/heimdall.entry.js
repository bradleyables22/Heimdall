import { createActionInvoker } from "./core/actions.js";
import { createBootTriggers } from "./core/boot-triggers.js";
import { createDiagnostics } from "./core/diagnostics.js";
import { createDomPipeline } from "./core/dom.js";
import { createEventDelegates } from "./core/event-delegates.js";
import { createJsInvokeVoidRuntime } from "./core/js-invoke-void.js";
import { createPayloadResolver } from "./core/payloads.js";
import { createRequestCoordinator } from "./core/request-coordinator.js";
import { createSecurityTokens } from "./core/security-tokens.js";
import { createHeimdallRuntime } from "./core/startup.js";
import { createSseRuntime } from "./core/sse.js";
import { createTimeLocalization } from "./core/time-localization.js";
import {
    onReady,
    safeText
} from "./core/utils.js";

(function (global) {
    "use strict";

    // ============================================================================
    // Heimdall runtime
    // ---------------------------------------------------------------------------
    // API Version: v1
    // ---------------------------------------------------------------------------
    // Endpoints
    // ---------------------------------------------------------------------------
    // Content Actions:
    //   POST /__heimdall/v1/content/actions
    //     - Executes a server action
    //     - Returns HTML (with optional <invocation>, <abort>, or <redirect> directives)
    //
    // CSRF Token:
    //   GET  /__heimdall/v1/csrf
    //     - Returns antiforgery token
    //     - Cached client-side and reused automatically
    //
    // Bifrost (Server-Sent Events):
    //   GET  /__heimdall/v1/bifrost?topic=...
    //     - Subscribes to a server topic
    //     - Streams HTML payloads and/or <invocation>, <abort>, or <redirect> directives
    //     - Protected via short-lived subscribe token (minted with CSRF)
    //
    // ---------------------------------------------------------------------------
    // Content Action Attributes
    // ---------------------------------------------------------------------------
    // Triggers:
    //   - heimdall-content-load="Action.Id"
    //   - heimdall-content-click="Action.Id"
    //   - heimdall-content-change="Action.Id"
    //   - heimdall-content-input="Action.Id"
    //   - heimdall-content-submit="Action.Id"
    //   - heimdall-content-keydown="Action.Id"
    //   - heimdall-content-blur="Action.Id"
    //   - heimdall-content-hover="Action.Id"
    //   - heimdall-content-visible="Action.Id"
    //   - heimdall-content-scroll="Action.Id"
    //
    // Common Options:
    //   - heimdall-content-target="#selector"
    //   - heimdall-content-swap="inner|outer|beforeend|afterbegin|none"
    //   - heimdall-content-disable="true|false"
    //   - heimdall-prevent-default="true|false"
    //   - heimdall-ignore="click|change|input|submit|keydown|blur|hover|visible|scroll|load|*"
    //   - heimdall-scope="closest|self"
    //
    // Payload Options:
    //   - heimdall-payload='{"json":1}'
    //   - heimdall-payload-from="closest-form|self|#form|ref:path|closest-state[:key]"
    //   - heimdall-payload-ref="Path.To.Object"
    //
    // Trigger Modifiers:
    //   - heimdall-debounce="ms"
    //   - heimdall-key="Enter|Escape|13"
    //   - heimdall-hover-delay="ms"
    //   - heimdall-visible-once="true|false"
    //   - heimdall-scroll-threshold="px"
    //   - heimdall-poll="ms"
    //   - heimdall-sync="parallel|replace|drop|queue-latest"
    //   - heimdall-sync-group="name"
    //
    // ---------------------------------------------------------------------------
    // Trigger Resolution Options
    // ---------------------------------------------------------------------------
    // These attributes control how Heimdall resolves triggers in nested DOM
    // structures. They affect framework event routing, not browser behavior.
    //
    // heimdall-ignore
    //   Prevents Heimdall from resolving triggers past this element for the
    //   specified trigger types.
    //
    //   Examples:
    //     heimdall-ignore="click"
    //     heimdall-ignore="click input change"
    //     heimdall-ignore="*"
    //
    //   Behavior:
    //     - Blocks outer trigger resolution
    //     - Triggers inside the ignored region still work
    //     - Applies only to Heimdall delegated triggers
    //
    // heimdall-scope
    //   Controls how an actionable element is matched when a trigger fires.
    //
    //   Values:
    //     closest (default) — nearest ancestor with trigger attribute
    //     self              — only fire when the element itself is the event target
    //
    //   Example:
    //     <div heimdall-content-click="close" heimdall-scope="self">
    //
    //   Useful for modal backdrops, overlays, and dismiss regions.
    //
    // ---------------------------------------------------------------------------
    // Response Directives (<invocation>, <abort>, <redirect>)
    // ---------------------------------------------------------------------------
    // Any <invocation> element returned by the server is treated as an instruction
    // and is never rendered directly into the response output.
    //
    // Required:
    //   - heimdall-content-target="#selector"
    //
    // Optional:
    //   - heimdall-content-swap="inner|outer|beforeend|afterbegin|none"
    //
    // Payload:
    //   - Wrap HTML fragments in <template> to preserve table rows (<tr>, etc)
    //
    // Security:
    //   - <script> tags are always stripped
    //
    // <abort>:
    //   - Suppresses the main target swap for the current response/payload
    //   - Still allows <invocation> directives to be processed
    //   - Optional reason attribute is surfaced through emitted abort events
    //
    // <redirect>:
    //   - Forces immediate browser navigation
    //   - Acts as a hard-stop directive
    //   - Prevents OOB processing, abort handling, and main target swap
    //   - First redirect wins
    //
    // ---------------------------------------------------------------------------
    // Bifrost (SSE) Attributes
    // ---------------------------------------------------------------------------
    //   - heimdall-sse="topic:name"
    //   - heimdall-sse-topic="topic:name"      (alias)
    //   - heimdall-sse-target="#selector"       (default: element itself)
    //   - heimdall-sse-swap="inner|outer|beforeend|afterbegin|none"
    //   - heimdall-sse-event="heimdall"
    //   - heimdall-sse-disable="true|false"
    //
    // Bifrost notes:
    //   - Uses EventSource (GET only)
    //   - Automatically reconnects
    //   - Designed for same-origin use
    //   - Subscription access is gated server-side
    //
    // ---------------------------------------------------------------------------
    // This file is intentionally dependency-free and framework-agnostic.
    // It is safe to use alongside Blazor, Razor Pages, MVC, or static HTML.
    //
    // CSRF Token:
    //   GET  /__heimdall/v1/csrf
    //     - Returns antiforgery token
    //     - Cached client-side and reused automatically
    //
    // Bifrost (Server-Sent Events):
    //   GET  /__heimdall/v1/bifrost?topic=...
    //     - Subscribes to a server topic
    //     - Streams HTML payloads and/or <invocation>, <abort>, or <redirect> directives
    //     - Protected via short-lived subscribe token (minted with CSRF)
    //
    // ---------------------------------------------------------------------------
    // Content Action Attributes
    // ---------------------------------------------------------------------------
    //
    // Triggers
    // ---------------------------------------------------------------------------
    // These attributes define actions that execute when the corresponding DOM
    // event occurs. Heimdall uses delegated event handling, so triggers work on
    // dynamically inserted content without rebinding.
    //
    //   - heimdall-content-load="Action.Id"
    //   - heimdall-content-click="Action.Id"
    //   - heimdall-content-change="Action.Id"
    //   - heimdall-content-input="Action.Id"
    //   - heimdall-content-submit="Action.Id"
    //   - heimdall-content-keydown="Action.Id"
    //   - heimdall-content-blur="Action.Id"
    //   - heimdall-content-hover="Action.Id"
    //   - heimdall-content-visible="Action.Id"
    //   - heimdall-content-scroll="Action.Id"
    //
    // ---------------------------------------------------------------------------
    // Trigger Resolution Options
    // ---------------------------------------------------------------------------
    // These attributes control how Heimdall resolves triggers in nested DOM
    // structures. They affect framework event routing, not browser behavior.
    //
    // heimdall-ignore
    //   Prevents Heimdall from resolving triggers past this element for the
    //   specified trigger types.
    //
    //   Examples:
    //     heimdall-ignore="click"
    //     heimdall-ignore="click input change"
    //     heimdall-ignore="*"
    //
    //   Behavior:
    //     - Blocks outer trigger resolution
    //     - Triggers inside the ignored region still work
    //     - Applies only to Heimdall delegated triggers
    //
    // heimdall-scope
    //   Controls how an actionable element is matched when a trigger fires.
    //
    //   Values:
    //     closest (default) � nearest ancestor with trigger attribute
    //     self              � only fire when the element itself is the event target
    //
    //   Example:
    //     <div heimdall-content-click="close" heimdall-scope="self">
    //
    //   Useful for modal backdrops, overlays, and dismiss regions.
    //
    // ---------------------------------------------------------------------------
    // Common Options
    // ---------------------------------------------------------------------------
    //   - heimdall-content-target="#selector"
    //   - heimdall-content-swap="inner|outer|beforeend|afterbegin|none"
    //   - heimdall-content-disable="true|false"
    //   - heimdall-prevent-default="true|false"
    //
    // ---------------------------------------------------------------------------
    // Payload Options
    // ---------------------------------------------------------------------------
    //   - heimdall-payload='{"json":1}'
    //   - heimdall-payload-from="closest-form|self|#form|ref:path|closest-state[:key]"
    //   - heimdall-payload-ref="Path.To.Object"
    //
    // ---------------------------------------------------------------------------
    // Trigger Modifiers
    // ---------------------------------------------------------------------------
    //   - heimdall-debounce="ms"
    //   - heimdall-key="Enter|Escape|13"
    //   - heimdall-hover-delay="ms"
    //   - heimdall-visible-once="true|false"
    //   - heimdall-scroll-threshold="px"
    //   - heimdall-poll="ms"
    //   - heimdall-sync="parallel|replace|drop|queue-latest"
    //   - heimdall-sync-group="name"
    //
    // ---------------------------------------------------------------------------
    // Response Directives (<invocation>, <abort>, <redirect>)
    // ---------------------------------------------------------------------------
    // Any <invocation> element returned by the server is treated as an instruction
    // and is never rendered directly into the response output.
    //
    // Required:
    //   - heimdall-content-target="#selector"
    //
    // Optional:
    //   - heimdall-content-swap="inner|outer|beforeend|afterbegin|none"
    //
    // Payload:
    //   - Wrap HTML fragments in <template> to preserve table rows (<tr>, etc)
    //
    // Security:
    //   - <script> tags are always stripped
    //
    // <abort>:
    //   - Suppresses the main target swap for the current response/payload
    //   - Still allows <invocation> directives to be processed
    //   - Optional reason attribute is surfaced through emitted abort events
    //
    // <redirect>:
    //   - Forces immediate browser navigation
    //   - Acts as a hard-stop directive
    //   - Prevents OOB processing, abort handling, and main target swap
    //   - First redirect wins
    //
    // ---------------------------------------------------------------------------
    // Bifrost (SSE) Attributes
    // ---------------------------------------------------------------------------
    //   - heimdall-sse="topic:name"
    //   - heimdall-sse-topic="topic:name"      (alias)
    //   - heimdall-sse-target="#selector"      (default: element itself)
    //   - heimdall-sse-swap="inner|outer|beforeend|afterbegin|none"
    //   - heimdall-sse-event="heimdall"
    //   - heimdall-sse-disable="true|false"
    //
    // Bifrost notes:
    //   - Uses EventSource (GET only)
    //   - Automatically reconnects
    //   - Designed for same-origin use
    //   - Subscription access is gated server-side
    //
    // ---------------------------------------------------------------------------
    // This file is intentionally dependency-free and framework-agnostic.
    // It is safe to use alongside Blazor, Razor Pages, MVC, or static HTML.
    //
    // ============================================================================

    const API_VERSION = 1;

    const DEFAULT_BASE_PATH = "/__heimdall";
    const DEFAULT_CONTENT_ENDPOINT = `${DEFAULT_BASE_PATH}/v${API_VERSION}/content/actions`;
    const DEFAULT_CSRF_ENDPOINT = `${DEFAULT_BASE_PATH}/v${API_VERSION}/csrf`;
    const DEFAULT_BIFROST_ENDPOINT = `${DEFAULT_BASE_PATH}/v${API_VERSION}/bifrost`;
    const DEFAULT_BIFROST_TOKEN_ENDPOINT = `${DEFAULT_BASE_PATH}/v${API_VERSION}/bifrost/token`;

    const ACTION_HEADER = "X-Heimdall-Content-Action";
    const CSRF_HEADER = "RequestVerificationToken";
    const runtimeRef = { current: null };
    const getRuntimeConfig = () => runtimeRef.current && runtimeRef.current.config;
    const { payloadFromElement } = createPayloadResolver(global);
    const { emit, emitLifecycle, dbg } = createDiagnostics(getRuntimeConfig);
    const coordinator = createRequestCoordinator({
        global,
        dbg
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
    const dom = createDomPipeline({
        getConfig: getRuntimeConfig,
        boot: root => boot(root),
        dbg,
        emitLifecycle,
        jsInvokeVoid,
        timeLocalization
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
        dbg,
        safeText,
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
        payloadFromElement,
        boot: root => boot(root),
        dom,
        coordinator,
        actionHeader: ACTION_HEADER,
        csrfHeader: CSRF_HEADER
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
        boot: root => boot(root),
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
        installSseSweeper,
        dbg,
        onRuntimeCreated: runtime => {
            runtimeRef.current = runtime;
        }
    });

})(window);
