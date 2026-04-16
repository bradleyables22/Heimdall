namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides helper methods for generating Bootstrap overflow utility CSS classes.
        /// </summary>
        /// <remarks>
        /// Use the members of this class to control how content overflow is handled within an element.
        /// These helpers generate the appropriate Bootstrap utility classes for general overflow as well
        /// as axis-specific overflow (<c>x</c> and <c>y</c>).
        /// 
        /// This is useful when dynamically composing layouts where scroll behavior or clipping must be
        /// applied conditionally.
        /// 
        /// This class is static and cannot be instantiated.
        /// </remarks>
        public static class Overflow
        {
            /// <summary>
            /// Returns the CSS class string for controlling overflow on both axes.
            /// </summary>
            /// <param name="kind">The overflow behavior to apply.</param>
            /// <returns>A string containing the Bootstrap overflow utility class.</returns>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Thrown if the specified overflow kind is not supported.
            /// </exception>
            public static string All(OverflowKind kind) => $"overflow-{OverflowToken(kind)}";

            /// <summary>
            /// Returns the CSS class string for controlling horizontal overflow.
            /// </summary>
            /// <param name="kind">The overflow behavior to apply along the horizontal axis.</param>
            /// <returns>A string containing the Bootstrap horizontal overflow utility class.</returns>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Thrown if the specified overflow kind is not supported.
            /// </exception>
            public static string X(OverflowKind kind) => $"overflow-x-{OverflowToken(kind)}";

            /// <summary>
            /// Returns the CSS class string for controlling vertical overflow.
            /// </summary>
            /// <param name="kind">The overflow behavior to apply along the vertical axis.</param>
            /// <returns>A string containing the Bootstrap vertical overflow utility class.</returns>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Thrown if the specified overflow kind is not supported.
            /// </exception>
            public static string Y(OverflowKind kind) => $"overflow-y-{OverflowToken(kind)}";

            private static string OverflowToken(OverflowKind k) => k switch
            {
                OverflowKind.Auto => "auto",
                OverflowKind.Hidden => "hidden",
                OverflowKind.Visible => "visible",
                OverflowKind.Scroll => "scroll",
                _ => throw new ArgumentOutOfRangeException(nameof(k))
            };
        }
    }
}