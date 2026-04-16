namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides Bootstrap layout-related CSS class names and helper methods for building responsive grid layouts.
        /// </summary>
        /// <remarks>This static class centralizes commonly used Bootstrap container, row, and column class names,
        /// along with utility methods for generating responsive grid, spacing, and offset classes. It is intended to
        /// simplify the process of constructing consistent and flexible layouts in UI components.</remarks>
        public static class Layout
        {
            public const string Container = "container";
            public const string ContainerFluid = "container-fluid";
            public const string ContainerSm = "container-sm";
            public const string ContainerMd = "container-md";
            public const string ContainerLg = "container-lg";
            public const string ContainerXl = "container-xl";
            public const string ContainerXxl = "container-xxl";

            public const string Row = "row";
            public const string Col = "col";
            public const string ColAuto = "col-auto";

            /// <summary>
            /// Returns the CSS class name for a column span of the specified size and optional breakpoint.
            /// </summary>
            /// <remarks>Use this method to generate Bootstrap column span classes such as <c>col-6</c> or
            /// <c>col-md-4</c>, typically for defining grid layouts.</remarks>
            /// <param name="span">The number of columns to span. Must be between 1 and 12.</param>
            /// <param name="bp">An optional breakpoint at which the column span should apply.</param>
            /// <returns>A string containing the CSS class name representing the column span.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if span is outside the valid range of 1 to 12.</exception>
            public static string ColSpan(int span, Breakpoint bp = Breakpoint.None)
            {
                if (span < 1 || span > 12)
                    throw new ArgumentOutOfRangeException(nameof(span), "Bootstrap columns are 1..12.");

                return bp == Breakpoint.None
                    ? $"col-{span}"
                    : $"col-{Bp(bp)}-{span}";
            }

            /// <summary>
            /// Returns the CSS class name for an auto-sized column at the specified breakpoint.
            /// </summary>
            /// <remarks>Use this method to generate responsive auto column classes such as <c>col-auto</c> or
            /// <c>col-lg-auto</c>, typically for content-sized columns.</remarks>
            /// <param name="bp">The breakpoint at which the auto column behavior should apply.</param>
            /// <returns>A string containing the CSS class name representing the auto column.</returns>
            public static string ColAutoAt(Breakpoint bp)
            {
                if (bp == Breakpoint.None)
                    return ColAuto;

                return $"col-{Bp(bp)}-auto";
            }

            /// <summary>
            /// Returns the CSS class name for setting the number of columns in a row, optionally at a given breakpoint.
            /// </summary>
            /// <remarks>Use this method to generate Bootstrap row column classes such as <c>row-cols-3</c> or
            /// <c>row-cols-md-4</c>, typically for evenly distributing columns within a row.</remarks>
            /// <param name="cols">The number of columns to display. Must be between 1 and 6.</param>
            /// <param name="bp">An optional breakpoint at which the column count should apply.</param>
            /// <returns>A string containing the CSS class name representing the row column configuration.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if cols is outside the valid range of 1 to 6.</exception>
            public static string RowCols(int cols, Breakpoint bp = Breakpoint.None)
            {
                if (cols < 1 || cols > 6)
                    throw new ArgumentOutOfRangeException(nameof(cols), "Bootstrap row-cols are typically 1..6.");

                return bp == Breakpoint.None
                    ? $"row-cols-{cols}"
                    : $"row-cols-{Bp(bp)}-{cols}";
            }

            /// <summary>
            /// Returns the CSS class name for offsetting a column by the specified span and optional breakpoint.
            /// </summary>
            /// <remarks>Use this method to generate Bootstrap offset classes such as <c>offset-2</c> or
            /// <c>offset-md-3</c>, typically for shifting columns within a grid layout.</remarks>
            /// <param name="span">The number of columns to offset. Must be between 0 and 11.</param>
            /// <param name="bp">An optional breakpoint at which the offset should apply.</param>
            /// <returns>A string containing the CSS class name representing the column offset.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if span is outside the valid range of 0 to 11.</exception>
            public static string Offset(int span, Breakpoint bp = Breakpoint.None)
            {
                if (span < 0 || span > 11)
                    throw new ArgumentOutOfRangeException(nameof(span), "Bootstrap offsets are 0..11.");

                return bp == Breakpoint.None
                    ? $"offset-{span}"
                    : $"offset-{Bp(bp)}-{span}";
            }

            /// <summary>
            /// Returns the CSS class name for applying uniform gutter spacing, optionally at a given breakpoint.
            /// </summary>
            /// <remarks>Use this method to generate Bootstrap gutter classes such as <c>g-3</c> or <c>g-lg-2</c>,
            /// typically for controlling spacing between columns and rows.</remarks>
            /// <param name="n">The gutter size. Must be between 0 and 5.</param>
            /// <param name="bp">An optional breakpoint at which the gutter should apply.</param>
            /// <returns>A string containing the CSS class name representing the gutter spacing.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if n is outside the valid range of 0 to 5.</exception>
            public static string Gutter(int n, Breakpoint bp = Breakpoint.None)
                => GutterInternal("g", n, bp);

            /// <summary>
            /// Returns the CSS class name for applying horizontal gutter spacing, optionally at a given breakpoint.
            /// </summary>
            /// <remarks>Use this method to generate Bootstrap horizontal gutter classes such as <c>gx-3</c> or
            /// <c>gx-md-2</c>.</remarks>
            /// <param name="n">The gutter size. Must be between 0 and 5.</param>
            /// <param name="bp">An optional breakpoint at which the gutter should apply.</param>
            /// <returns>A string containing the CSS class name representing horizontal gutter spacing.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if n is outside the valid range of 0 to 5.</exception>
            public static string GutterX(int n, Breakpoint bp = Breakpoint.None)
                => GutterInternal("gx", n, bp);

            /// <summary>
            /// Returns the CSS class name for applying vertical gutter spacing, optionally at a given breakpoint.
            /// </summary>
            /// <remarks>Use this method to generate Bootstrap vertical gutter classes such as <c>gy-3</c> or
            /// <c>gy-md-2</c>.</remarks>
            /// <param name="n">The gutter size. Must be between 0 and 5.</param>
            /// <param name="bp">An optional breakpoint at which the gutter should apply.</param>
            /// <returns>A string containing the CSS class name representing vertical gutter spacing.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if n is outside the valid range of 0 to 5.</exception>
            public static string GutterY(int n, Breakpoint bp = Breakpoint.None)
                => GutterInternal("gy", n, bp);

            private static string GutterInternal(string prefix, int n, Breakpoint bp)
            {
                if (n < 0 || n > 5)
                    throw new ArgumentOutOfRangeException(nameof(n), "Bootstrap gutter scale is 0..5.");

                return bp == Breakpoint.None
                    ? $"{prefix}-{n}"
                    : $"{prefix}-{Bp(bp)}-{n}";
            }
        }
    }
}