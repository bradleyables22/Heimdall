namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides constants and helper methods for working with Bootstrap alert CSS classes.
        /// </summary>
        /// <remarks>Use the members of this class to reference standard Bootstrap alert class names or to
        /// generate alert class names dynamically based on color variants. This class is intended to simplify the
        /// creation and management of alert components in applications that use Bootstrap for styling.</remarks>
        public static class Alert
        {
            public const string Base = "alert";
            public const string Dismissible = "alert-dismissible";
            public const string Heading = "alert-heading";
            public const string Link = "alert-link";


            /// <summary>
            /// Returns the CSS class name corresponding to the specified alert color variant.
            /// </summary>
            /// <remarks>Use this method to map a Color value to its corresponding Bootstrap alert CSS
            /// class. This is useful when dynamically generating alert components based on color.</remarks>
            /// <param name="c">The alert color for which to retrieve the CSS class name. Must be a defined value of the Color
            /// enumeration.</param>
            /// <returns>A string containing the CSS class name for the specified alert color variant.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if the specified color is not a supported value of the Color enumeration.</exception>
            public static string Variant(Color c) => c switch
            {
                Color.Primary => "alert alert-primary",
                Color.Secondary => "alert alert-secondary",
                Color.Success => "alert alert-success",
                Color.Danger => "alert alert-danger",
                Color.Warning => "alert alert-warning",
                Color.Info => "alert alert-info",
                Color.Light => "alert alert-light",
                Color.Dark => "alert alert-dark",
                _ => throw new ArgumentOutOfRangeException(nameof(c), $"Color '{c}' is not supported for alert variant.")
            };
        }
    }
}
