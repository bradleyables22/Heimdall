namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides predefined CSS class names and helper methods for Bootstrap-styled buttons.
        /// </summary>
        /// <remarks>This static class centralizes commonly used Bootstrap button class names, including
        /// solid and outline variants, sizes, and state-related classes. It also offers a helper method to generate the
        /// appropriate class name for a given color and style. Use these constants and methods to ensure consistency
        /// and reduce errors when applying Bootstrap button styles in your application.</remarks>
        public static class Btn
        {
            public const string Base = "btn";

            // Solid variants
            public const string Primary = "btn btn-primary";
            public const string Secondary = "btn btn-secondary";
            public const string Success = "btn btn-success";
            public const string Danger = "btn btn-danger";
            public const string Warning = "btn btn-warning";
            public const string Info = "btn btn-info";
            public const string Light = "btn btn-light";
            public const string Dark = "btn btn-dark";
            public const string Link = "btn btn-link";
            public const string Close = "btn btn-close";

            // Outline variants
            public const string OutlinePrimary = "btn btn-outline-primary";
            public const string OutlineSecondary = "btn btn-outline-secondary";
            public const string OutlineSuccess = "btn btn-outline-success";
            public const string OutlineDanger = "btn btn-outline-danger";
            public const string OutlineWarning = "btn btn-outline-warning";
            public const string OutlineInfo = "btn btn-outline-info";
            public const string OutlineLight = "btn btn-outline-light";
            public const string OutlineDark = "btn btn-outline-dark";

            // Sizes
            public const string Sm = "btn-sm";
            public const string Lg = "btn-lg";

            // States / layouts
            public const string Disabled = "disabled";
            public const string Active = "active";
            public const string Group = "btn-group";
            public const string GroupVertical = "btn-group-vertical";
            public const string Toolbar = "btn-toolbar";

            /// <summary>
            /// Returns the CSS class name for the specified button color variant, optionally as an outline style.
            /// </summary>
            /// <remarks>Use this method to obtain the appropriate CSS class for styling buttons based
            /// on color and outline preference. The returned class name can be applied directly to HTML elements to
            /// achieve the desired button appearance.</remarks>
            /// <param name="c">The button color variant for which to retrieve the CSS class name.</param>
            /// <param name="outline">true to return the outline style class name; otherwise, false to return the standard style. The default
            /// is false.</param>
            /// <returns>A string containing the CSS class name corresponding to the specified color and style.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if c is not a supported Color value.</exception>
            public static string Variant(Color c, bool outline = false)
            {
                if (!outline)
                {
                    return c switch
                    {
                        Color.Primary => Primary,
                        Color.Secondary => Secondary,
                        Color.Success => Success,
                        Color.Danger => Danger,
                        Color.Warning => Warning,
                        Color.Info => Info,
                        Color.Light => Light,
                        Color.Dark => Dark,
                        _ => throw new ArgumentOutOfRangeException(nameof(c), $"Color '{c}' is not supported for button variant.")
                    };
                }

                return c switch
                {
                    Color.Primary => OutlinePrimary,
                    Color.Secondary => OutlineSecondary,
                    Color.Success => OutlineSuccess,
                    Color.Danger => OutlineDanger,
                    Color.Warning => OutlineWarning,
                    Color.Info => OutlineInfo,
                    Color.Light => OutlineLight,
                    Color.Dark => OutlineDark,
                    _ => throw new ArgumentOutOfRangeException(nameof(c), $"Color '{c}' is not supported for button outline variant.")
                };
            }
        }
    }
}
