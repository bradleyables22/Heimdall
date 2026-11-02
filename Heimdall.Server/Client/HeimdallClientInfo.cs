using System.Text.Json;

namespace Heimdall.Server
{
    /// <summary>
    /// Contains explicitly allowlisted browser storage values reported with a content action.
    /// Every value is supplied by the client and must be treated as untrusted input.
    /// </summary>
    public sealed class HeimdallClientStorage
    {
        private Dictionary<string, string> _local = new(StringComparer.Ordinal);
        private Dictionary<string, string> _session = new(StringComparer.Ordinal);

        /// <summary>Gets allowlisted local-storage values.</summary>
        public Dictionary<string, string> Local
        {
            get => _local;
            init => _local = value ?? new(StringComparer.Ordinal);
        }

        /// <summary>Gets allowlisted session-storage values.</summary>
        public Dictionary<string, string> Session
        {
            get => _session;
            init => _session = value ?? new(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Configures server-side limits for the optional client-information storage and extension fields.
    /// The existing 4096-character client-information header limit remains authoritative.
    /// </summary>
    public sealed class HeimdallClientInfoLimits
    {
        /// <summary>Maximum number of storage values accepted in either storage area.</summary>
        public int MaxStorageKeysPerArea { get; set; } = 16;

        /// <summary>Maximum storage key length in characters.</summary>
        public int MaxStorageKeyLength { get; set; } = 128;

        /// <summary>Maximum storage value length in characters.</summary>
        public int MaxStorageValueLength { get; set; } = 1024;

        /// <summary>Maximum serialized storage length in characters.</summary>
        public int MaxStorageChars { get; set; } = 2048;

        /// <summary>Maximum number of extension properties accepted.</summary>
        public int MaxExtensionEntries { get; set; } = 16;

        /// <summary>Maximum serialized extension length in characters.</summary>
        public int MaxExtensionChars { get; set; } = 1024;

        /// <summary>Maximum JSON nesting depth accepted in extensions.</summary>
        public int MaxExtensionDepth { get; set; } = 4;
    }

    /// <summary>
    /// Describes presentation-oriented browser capabilities reported with a Heimdall content action.
    /// Every value is supplied by the client and must be treated as untrusted input.
    /// </summary>
    public sealed class HeimdallClientInfo
    {
        private string[] _languages = [];
        private HeimdallClientStorage _storage = new();
        private Dictionary<string, JsonElement> _extensions = new(StringComparer.Ordinal);

        /// <summary>The request header used by the Heimdall browser runtime.</summary>
        public const string HeaderName = "X-Heimdall-Client-Info";

        /// <summary>The maximum accepted serialized header length.</summary>
        public const int MaxHeaderLength = 4096;

        /// <summary>Whether the request contained a client-information header.</summary>
        public bool IsAvailable { get; internal set; }

        /// <summary>Gets the explicitly allowlisted browser storage values.</summary>
        public HeimdallClientStorage Storage
        {
            get => _storage;
            init => _storage = value ?? new();
        }

        /// <summary>Gets small application-defined client hints.</summary>
        public Dictionary<string, JsonElement> Extensions
        {
            get => _extensions;
            init => _extensions = value ?? new(StringComparer.Ordinal);
        }

        /// <summary>The browser's IANA timezone identifier, when available.</summary>
        public string? TimeZone { get; init; }

        /// <summary>The browser's current offset from UTC in minutes.</summary>
        public int UtcOffsetMinutes { get; init; }

        /// <summary>The browser's primary locale.</summary>
        public string? Locale { get; init; }

        /// <summary>The browser's ordered language preferences.</summary>
        public string[] Languages
        {
            get => _languages;
            init => _languages = value ?? [];
        }

        /// <summary>The visual viewport width in CSS pixels.</summary>
        public double ViewportWidth { get; init; }

        /// <summary>The visual viewport height in CSS pixels.</summary>
        public double ViewportHeight { get; init; }

        /// <summary>The screen width in CSS pixels.</summary>
        public int ScreenWidth { get; init; }

        /// <summary>The screen height in CSS pixels.</summary>
        public int ScreenHeight { get; init; }

        /// <summary>The ratio between physical pixels and CSS pixels.</summary>
        public double DevicePixelRatio { get; init; }

        /// <summary>The current viewport orientation: portrait or landscape.</summary>
        public string? Orientation { get; init; }

        /// <summary>A heuristic mobile, tablet, or desktop classification.</summary>
        public string? DeviceCategory { get; init; }

        /// <summary>The preferred color scheme: light, dark, or no-preference.</summary>
        public string? ColorScheme { get; init; }

        /// <summary>Whether the browser reports a reduced-motion preference.</summary>
        public bool PrefersReducedMotion { get; init; }

        /// <summary>The browser's contrast preference.</summary>
        public string? PrefersContrast { get; init; }

        /// <summary>Whether forced colors are active.</summary>
        public bool ForcedColors { get; init; }

        /// <summary>Whether touch input appears to be available.</summary>
        public bool Touch { get; init; }

        /// <summary>The maximum number of simultaneous touch contacts reported by the browser.</summary>
        public int MaxTouchPoints { get; init; }

        /// <summary>The primary pointer precision: fine, coarse, or none.</summary>
        public string? Pointer { get; init; }

        /// <summary>Whether the primary input mechanism supports hover.</summary>
        public bool Hover { get; init; }

        /// <summary>The browser's current online hint.</summary>
        public bool Online { get; init; }
    }
}
