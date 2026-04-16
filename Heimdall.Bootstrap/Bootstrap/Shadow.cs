namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides constants and helper methods for working with Bootstrap shadow utility CSS classes.
        /// </summary>
        /// <remarks>
        /// Use the members of this class to apply predefined shadow styles to elements.
        /// These utilities allow you to control the elevation and visual depth of components
        /// through varying shadow intensities.
        /// 
        /// The <see cref="Kind(ShadowKind)"/> method can be used to dynamically select a shadow style
        /// based on a strongly-typed value.
        /// 
        /// This class is static and cannot be instantiated.
        /// </remarks>
        public static class Shadow
        {
            public const string None = "shadow-none";
            public const string Sm = "shadow-sm";
            public const string Default = "shadow";
            public const string Lg = "shadow-lg";

            /// <summary>
            /// Returns the CSS class string corresponding to the specified shadow kind.
            /// </summary>
            /// <param name="k">The shadow style to apply. Must be a defined value of the ShadowKind enumeration.</param>
            /// <returns>A string containing the Bootstrap shadow utility class.</returns>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Thrown if the specified shadow kind is not a supported value.
            /// </exception>
            public static string Kind(ShadowKind k) => k switch
            {
                ShadowKind.None => None,
                ShadowKind.Sm => Sm,
                ShadowKind.Default => Default,
                ShadowKind.Lg => Lg,
                _ => throw new ArgumentOutOfRangeException(nameof(k))
            };
        }
    }
}