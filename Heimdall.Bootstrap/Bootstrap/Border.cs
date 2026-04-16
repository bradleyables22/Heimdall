namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides a set of constants and utility methods for generating Bootstrap border and border-radius CSS class
        /// names.
        /// </summary>
        /// <remarks>This static class centralizes commonly used Bootstrap border-related class names and
        /// helper methods for constructing border and border-radius classes dynamically. It is intended to simplify the
        /// application of consistent border styles in UI components that use Bootstrap. All members are thread-safe and
        /// can be used without instantiating the class.</remarks>
        public static class Border
        {
            public const string Default = "border";
            public const string None = "border-0";
            public const string Top = "border-top";
            public const string Top0 = "border-top-0";
            public const string Bottom = "border-bottom";
            public const string Bottom0 = "border-bottom-0";
            public const string Start = "border-start";
            public const string Start0 = "border-start-0";
            public const string End = "border-end";
            public const string End0 = "border-end-0";

            public const string Width1 = "border-1";
            public const string Width2 = "border-2";
            public const string Width3 = "border-3";
            public const string Width4 = "border-4";
            public const string Width5 = "border-5";

            /// <summary>
            /// Returns the CSS class name for the specified border color.
            /// </summary>
            /// <param name="c">The color value for which to retrieve the corresponding border CSS class. Must be a defined member of
            /// the Color enumeration.</param>
            /// <returns>A string containing the CSS class name that represents the specified border color.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if the specified color is not a supported value of the Color enumeration.</exception>
            public static string BorderColor(Color c) => c switch
            {
                Color.Primary => "border-primary",
                Color.Secondary => "border-secondary",
                Color.Success => "border-success",
                Color.Danger => "border-danger",
                Color.Warning => "border-warning",
                Color.Info => "border-info",
                Color.Light => "border-light",
                Color.Dark => "border-dark",
                _ => throw new ArgumentOutOfRangeException(nameof(c), $"Color '{c}' is not supported for border color.")
            };

            public const string Opacity10 = "border-opacity-10";
            public const string Opacity25 = "border-opacity-25";
            public const string Opacity50 = "border-opacity-50";
            public const string Opacity75 = "border-opacity-75";
            public const string Opacity100 = "border-opacity-100";


            public const string Rounded0 = "rounded-0";
            public const string Rounded1 = "rounded-1";
            public const string Rounded2 = "rounded-2";
            public const string Rounded3 = "rounded-3";
            public const string Rounded4 = "rounded-4";
            public const string Rounded5 = "rounded-5";
            public const string Rounded = "rounded";
            public const string RoundedPill = "rounded-pill";
            public const string RoundedCircle = "rounded-circle";

            public const string RoundedTop = "rounded-top";
            public const string RoundedBottom = "rounded-bottom";
            public const string RoundedStart = "rounded-start";
            public const string RoundedEnd = "rounded-end";

            public const string RoundedTop0 = "rounded-top-0";
            public const string RoundedBottom0 = "rounded-bottom-0";
            public const string RoundedStart0 = "rounded-start-0";
            public const string RoundedEnd0 = "rounded-end-0";

            /// <summary>
            /// Returns the Bootstrap CSS class name for the specified rounded scale value.
            /// </summary>
            /// <param name="n">The rounded scale value. Must be between 0 and 5, inclusive.</param>
            /// <returns>A string containing the Bootstrap rounded class name corresponding to the specified scale. Returns
            /// "rounded-0" if the value is 0; otherwise, returns "rounded-n" where n is the specified value.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown when the specified value is less than 0 or greater than 5.</exception>
            public static string Round(int n)
            {
                if (n < 0 || n > 5)
                    throw new ArgumentOutOfRangeException(nameof(n), "Bootstrap rounded scale is 0..5.");

                return n == 0 ? Rounded0 : $"rounded-{n}";
            }
            /// <summary>
            /// Returns the Bootstrap CSS class name that corresponds to the specified rounded style.
            /// </summary>
            /// <param name="kind">The rounded style to convert to a Bootstrap CSS class name.</param>
            /// <returns>A string containing the Bootstrap CSS class name for the specified rounded style.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if the value of kind is not a valid RoundedKind enumeration value.</exception>
            public static string RoundedKind(RoundedKind kind) => kind switch
            {
                Bootstrap.RoundedKind.None => Rounded0,
                Bootstrap.RoundedKind.Sm => "rounded-1",
                Bootstrap.RoundedKind.Default => Rounded,
                Bootstrap.RoundedKind.Lg => "rounded-3",
                Bootstrap.RoundedKind.Xl => "rounded-4",
                Bootstrap.RoundedKind.Xxl => "rounded-5",
                Bootstrap.RoundedKind.Pill => "rounded-pill",
                Bootstrap.RoundedKind.Circle => "rounded-circle",
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }
    }
}
