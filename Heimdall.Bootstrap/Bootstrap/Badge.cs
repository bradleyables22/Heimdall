namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides constants and helper methods for working with Bootstrap badge CSS classes.
        /// </summary>
        /// <remarks>Use the members of this class to generate or reference Bootstrap badge styles in UI
        /// components. This class is static and cannot be instantiated.</remarks>
        public static class Badge
        {
            public const string Base = "badge";
            public const string Pill = "rounded-pill";

            /// <summary>
            ///  Returns the CSS class string corresponding to the specified badge color variant.
            /// </summary>
            /// <remarks>Use this method to obtain the appropriate Bootstrap badge CSS class for a
            /// given color variant. This is useful when dynamically generating badge elements in a UI.</remarks>
            /// <param name="c">The badge color variant for which to retrieve the CSS class. Must be a defined value of the Color
            /// enumeration.</param>
            /// <returns>A string containing the CSS classes for the specified badge color variant.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if the specified color is not a supported value of the Color enumeration.</exception>
            public static string Variant(Color c) => c switch
            {
                Color.Primary => "badge text-bg-primary",
                Color.Secondary => "badge text-bg-secondary",
                Color.Success => "badge text-bg-success",
                Color.Danger => "badge text-bg-danger",
                Color.Warning => "badge text-bg-warning",
                Color.Info => "badge text-bg-info",
                Color.Light => "badge text-bg-light",
                Color.Dark => "badge text-bg-dark",
                _ => throw new ArgumentOutOfRangeException(nameof(c), $"Color '{c}' is not supported for badge variant.")
            };
        }
    }
}
