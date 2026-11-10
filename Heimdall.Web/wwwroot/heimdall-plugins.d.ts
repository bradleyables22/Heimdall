/** A canonical behavior attribute or a named-state prefix (ending in -*). */
export interface AttributeDefinition {
    readonly name: string;
    readonly owner: string;
    readonly category: "trigger" | "content" | "payload" | "modifier" | "sse" | "time" | "state" | "plugin-trigger";
    readonly kind: "attribute" | "prefix";
    readonly aliases: readonly string[];
}

export interface AttributeRegistry {
    list(): readonly AttributeDefinition[];
    /** Originals remain valid. Returns an idempotent function that removes this registration. */
    alias(mappings: Readonly<Record<string, string | readonly string[]>>, options?: {
        /** Explicitly reassign an existing alias; disposing restores its previous live mapping. */
        replace?: boolean;
    }): () => void;
    /** Original first, then its active aliases. Named state accepts a concrete key suffix. */
    names(original: string): readonly string[];
    get(element: Element, original: string): string | null;
    has(element: Element, original: string): boolean;
    selector(original: string): string;
}

/** Public frontend extension contract. Plugin API versions are independent of the HTTP API version. */
export interface HeimdallPlugin<TOptions = void> {
    readonly id: string;
    readonly version?: string;
    readonly pluginApiVersion: 1;
    readonly requires?: readonly string[];
    install(api: PluginApi, options: TOptions): void | (() => void);
}

export interface PluginApi {
    readonly pluginId: string;
    readonly pluginApiVersion: 1;
    /** Aborted when installation rolls back or the plugin is removed. */
    readonly signal: AbortSignal;
    /** Aliases registered here are owned by this plugin and removed on rollback/disposal. */
    readonly attributes: AttributeRegistry;
    readonly behaviors: { register(definition: BehaviorDefinition): string };
    readonly triggers: { register(definition: TriggerDefinition): string };
    readonly payloads: { register(definition: PayloadDefinition): string };
    readonly commands: CommandRegistry;
    invoke(invocationId: string, payload?: unknown | FormData, options?: InvokeOptions): Promise<InvokeResult>;
    onDispose(cleanup: () => void): void;
}

export interface BehaviorContext {
    readonly element: Element;
    readonly pluginId: string;
    readonly options: unknown;
    readonly signal: AbortSignal;
}

export interface BehaviorInstance {
    dispose?(): void;
    /** Synchronous update for changes in observedAttributes; omit to dispose and remount instead. */
    update?(context: { element: Element; signal: AbortSignal }): void;
}

export type BehaviorMountResult = void | (() => void) | BehaviorInstance;

export interface BehaviorDefinition {
    /** Local name; the registry prefixes the owning plugin id. */
    name: string;
    selector: string;
    observedAttributes?: readonly string[];
    mount(element: Element, context: BehaviorContext): BehaviorMountResult | Promise<BehaviorMountResult>;
}

export interface TriggerContext {
    readonly element: Element;
    readonly signal: AbortSignal;
    /** Submitted payloads are snapshotted; omitting payload uses the element's payload attributes. */
    emit(event?: Event | null, overrides?: { payload?: unknown | FormData }): Promise<InvokeResult | null | undefined>;
}

export interface TriggerDefinition {
    name: string;
    /** Defaults to heimdall-content-{plugin-id}-{local-name}, with dots replaced by hyphens. */
    attribute?: string;
    /** Additional spellings for this trigger, registered in the shared core registry. */
    aliases?: readonly string[];
    event?: string;
    events?: readonly string[];
    subscribe?(context: TriggerContext): void | (() => void);
    filter?(context: { element: Element; event: Event | null }): boolean;
    preventDefault?: boolean;
    debounceMs?: number;
}

export interface PayloadDefinition {
    name: string;
    /** Trigger snapshots after debounce; execution re-reads when queued work begins. */
    timing?: "trigger" | "execution";
    read(context: { element: Element; options: unknown }): unknown | FormData;
}

export interface CommandContext<T = unknown> {
    readonly element: Element | null;
    readonly args: T;
    readonly signal: AbortSignal;
    readonly pluginId: string;
    readonly invocationId: string | null;
    readonly sourceElement: Element | null;
    readonly origin: "invocation" | "sse";
    readonly timing: "before" | "after";
}

export interface CommandDefinition<T = unknown> {
    name: string;
    validate?(args: unknown): boolean;
    handle(context: CommandContext<T>): void | Promise<void>;
}

export interface CommandRegistry {
    register<T>(definition: CommandDefinition<T> & { validate(args: unknown): args is T }): string;
    register<T = unknown>(definition: CommandDefinition<T>): string;
}

export interface InstalledPlugin {
    readonly id: string;
    readonly version: string | null;
    readonly pluginApiVersion: 1;
    readonly requires: readonly string[];
}

export interface PluginErrorDetail {
    pluginId: string | null;
    extension: string | null;
    phase: "install" | "mount" | "update" | "dispose" | "subscribe" | "trigger" | "payload" | "command";
    code: string;
    error: unknown;
    invocationId?: string | null;
    sourceElement?: Element | null;
}

export interface InvokeOptions {
    target?: string | Element;
    swap?: "inner" | "outer" | "beforeend" | "afterbegin" | "none";
    sourceEl?: Element;
    sync?: "parallel" | "replace" | "drop" | "queue-latest";
    syncGroup?: string;
    timeoutMs?: number;
    signal?: AbortSignal;
    headers?: Record<string, string>;
    endpoint?: string;
    onSuccess?(result: InvokeResult): void;
    onError?(result: InvokeResult): void;
}

export interface InvokeResult {
    ok: boolean;
    status: number;
    cancelled?: boolean;
    cancelReason?: string;
    requestId: number;
    html: string | null;
    error: string | null;
    response: Response | null;
    ms: number;
    [key: string]: unknown;
}

export interface PluginHost {
    readonly pluginApiVersion: 1;
    readonly apiVersion: number;
    readonly attributes: AttributeRegistry;
    use<TOptions>(plugin: HeimdallPlugin<TOptions>, ...options: TOptions extends void ? [options?: TOptions] : [options: TOptions]): this;
    readonly plugins: {
        list(): readonly InstalledPlugin[];
        remove(id: string): boolean;
    };
    boot(root: ParentNode): void;
    dispose(root: Node): void;
    onReady(callback: () => void): void;
    clearCsrfToken(): void;
    invoke: PluginApi["invoke"];
    config: {
        /** Applied to invocations, token requests, and EventSource. Configure before boot. */
        credentials: "same-origin" | "include";
        pluginMountTimeoutMs: number;
        pluginCommandTimeoutMs: number;
        pluginCommandMaxArgsChars: number;
        pluginCommandMaxCount: number;
        pluginSseQueueLimit: number;
        [key: string]: unknown;
    };
    /** Other runtime features are documented by the core runtime. */
    sse: {
        connect(topic: string, options?: Record<string, unknown>): unknown;
        disconnect(element: Element): void;
        disconnectAll(): void;
    };
}

declare global {
    const Heimdall: PluginHost;
    interface Window { Heimdall: PluginHost; }
    interface DocumentEventMap { "heimdall:plugin-error": CustomEvent<PluginErrorDetail>; }
}
