namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides Bootstrap navbar-related CSS class names and helper methods for building responsive navigation bars.
        /// </summary>
        /// <remarks>This static class centralizes commonly used Bootstrap navbar structure, styling, and behavior
        /// class names. It also provides helper methods for generating responsive expansion classes, enabling consistent
        /// and flexible navbar implementations across UI components.</remarks>
        public static class Navbar
        {
            public const string Base = "navbar";
            public const string Brand = "navbar-brand";
            public const string Toggler = "navbar-toggler";
            public const string TogglerIcon = "navbar-toggler-icon";
            public const string Nav = "navbar-nav";
            public const string Text = "navbar-text";
            public const string Collapse = "navbar-collapse";

            /// <summary>
            /// Returns the CSS class name for controlling navbar expansion at the specified breakpoint.
            /// </summary>
            /// <remarks>Use this method to generate responsive navbar expansion classes such as
            /// <c>navbar-expand</c> or <c>navbar-expand-lg</c>, typically for controlling when the navbar switches
            /// between collapsed and expanded states.</remarks>
            /// <param name="bp">The breakpoint at which the navbar should expand.</param>
            /// <returns>A string containing the CSS class name representing the navbar expansion behavior.</returns>
            public static string Expand(Breakpoint bp)
            {
                if (bp == Breakpoint.None) return "navbar-expand";
                return $"navbar-expand-{Bp(bp)}";
            }

            public const string Light = "navbar-light";
            public const string Dark = "navbar-dark";
        }
    }
}