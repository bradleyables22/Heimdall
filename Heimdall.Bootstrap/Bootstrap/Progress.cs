namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides constants and helper methods for working with Bootstrap progress bar CSS classes.
        /// </summary>
        /// <remarks>
        /// Use the members of this class to construct and style progress components, including the
        /// container, progress bar, and optional visual enhancements such as striped and animated states.
        /// 
        /// The <see cref="Variant(Color)"/> method can be used to apply contextual color styles to the
        /// progress bar when dynamically generating UI elements.
        /// 
        /// This class is static and cannot be instantiated.
        /// </remarks>
        public static class Progress
        {
            public const string Base = "progress";
            public const string Bar = "progress-bar";
            public const string Striped = "progress-bar-striped";
            public const string Animated = "progress-bar-animated";

            /// <summary>
            /// Returns the CSS class string corresponding to the specified progress bar color variant.
            /// </summary>
            /// <param name="c">The color variant to apply to the progress bar. Must be a defined value of the Color enumeration.</param>
            /// <returns>A string containing the Bootstrap background color utility class for the progress bar.</returns>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Thrown if the specified color is not a supported value of the Color enumeration.
            /// </exception>
            public static string Variant(Color c) => c switch
            {
                Color.Primary => "bg-primary",
                Color.Secondary => "bg-secondary",
                Color.Success => "bg-success",
                Color.Danger => "bg-danger",
                Color.Warning => "bg-warning",
                Color.Info => "bg-info",
                Color.Light => "bg-light",
                Color.Dark => "bg-dark",
                _ => throw new ArgumentOutOfRangeException(nameof(c), $"Color '{c}' is not supported for progress variant.")
            };
        }
    }
}