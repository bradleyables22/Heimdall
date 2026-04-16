namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides constants and helper methods for working with Bootstrap spinner CSS classes.
        /// </summary>
        /// <remarks>
        /// Use the members of this class to construct and style loading indicators using Bootstrap spinners.
        /// This includes both border and grow spinner types, as well as size variations.
        /// 
        /// The <see cref="Variant(Color)"/> method can be used to apply contextual color styles to the spinner
        /// when dynamically generating UI elements.
        /// 
        /// This class is static and cannot be instantiated.
        /// </remarks>
        public static class Spinner
        {
            public const string Border = "spinner-border";
            public const string Grow = "spinner-grow";
            public const string Sm = "spinner-border spinner-border-sm";
            public const string GrowSm = "spinner-grow spinner-grow-sm";

            /// <summary>
            /// Returns the CSS class string corresponding to the specified spinner color variant.
            /// </summary>
            /// <param name="c">The color variant to apply to the spinner. Must be a defined value of the Color enumeration.</param>
            /// <returns>A string containing the Bootstrap text color utility class for the spinner.</returns>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Thrown if the specified color is not a supported value of the Color enumeration.
            /// </exception>
            public static string Variant(Color c) => c switch
            {
                Color.Primary => "text-primary",
                Color.Secondary => "text-secondary",
                Color.Success => "text-success",
                Color.Danger => "text-danger",
                Color.Warning => "text-warning",
                Color.Info => "text-info",
                Color.Light => "text-light",
                Color.Dark => "text-dark",
                _ => throw new ArgumentOutOfRangeException(nameof(c), $"Color '{c}' is not supported for spinner variant.")
            };
        }
    }
}