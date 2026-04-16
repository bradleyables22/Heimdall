namespace Heimdall.Bootstrap
{

    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides constants and helper methods for working with Bootstrap table CSS classes.
        /// </summary>
        /// <remarks>
        /// Use the members of this class to construct and style table components, including layout,
        /// responsiveness, visual variants, and common table behaviors such as striping, hover states,
        /// and borders.
        /// 
        /// The helper methods enable dynamic generation of responsive and contextual table styles
        /// using strongly-typed values.
        /// 
        /// This class is static and cannot be instantiated.
        /// </remarks>
        public static class Table
        {
            public const string Base = "table";
            public const string Sm = "table-sm";
            public const string Striped = "table-striped";
            public const string StripedColumns = "table-striped-columns";
            public const string Hover = "table-hover";
            public const string Bordered = "table-bordered";
            public const string Borderless = "table-borderless";
            public const string Active = "table-active";
            public const string Responsive = "table-responsive";

            /// <summary>
            /// Returns the CSS class string for creating a responsive table at the specified breakpoint.
            /// </summary>
            /// <param name="bp">The breakpoint at which the table becomes horizontally scrollable.</param>
            /// <returns>
            /// A string containing the Bootstrap responsive table class. If no breakpoint is specified,
            /// the base responsive class is returned.
            /// </returns>
            public static string ResponsiveAt(Breakpoint bp)
            {
                if (bp == Breakpoint.None)
                    return Responsive;

                return $"table-responsive-{Bp(bp)}";
            }

            /// <summary>
            /// Returns the CSS class string corresponding to the specified table color variant.
            /// </summary>
            /// <param name="c">The table color variant to apply. Must be a defined value of the Color enumeration.</param>
            /// <returns>A string containing the Bootstrap table variant class.</returns>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Thrown if the specified color is not a supported value of the Color enumeration.
            /// </exception>
            public static string Variant(Color c) => c switch
            {
                Color.Primary => "table-primary",
                Color.Secondary => "table-secondary",
                Color.Success => "table-success",
                Color.Danger => "table-danger",
                Color.Warning => "table-warning",
                Color.Info => "table-info",
                Color.Light => "table-light",
                Color.Dark => "table-dark",
                _ => throw new ArgumentOutOfRangeException(nameof(c), $"Color '{c}' is not supported for table variant.")
            };
        }
    }
}