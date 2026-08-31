namespace Heimdall.Server
{
    /// <summary>
    /// Describes presentation-oriented browser capabilities reported with a Heimdall content action.
    /// Every value is supplied by the client and must be treated as untrusted input.
    /// </summary>
    public sealed class HeimdallClientInfo
    {
        private string[] _languages = [];

        /// <summary>The request header used by the Heimdall browser runtime.</summary>
        public const string HeaderName = "X-Heimdall-Client-Info";

        /// <summary>The maximum accepted serialized header length.</summary>
        public const int MaxHeaderLength = 4096;

        /// <summary>Whether the request contained a client-information header.</summary>
        public bool IsAvailable { get; internal set; }

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
