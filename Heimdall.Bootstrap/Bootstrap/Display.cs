namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides Bootstrap display-related CSS class names and helper methods for generating display utility
        /// classes based on display kind and breakpoint values.
        /// </summary>
        /// <remarks>This static class centralizes commonly used Bootstrap display and display heading class
        /// names, as well as utility methods for mapping display-related enumeration values to their corresponding CSS
        /// class names. It is intended to simplify the process of applying consistent display styles in UI components.</remarks>
        public static class Display
        {
            public const string Block = "d-block";
            public const string Inline = "d-inline";
            public const string InlineBlock = "d-inline-block";
            public const string Grid = "d-grid";
            public const string InlineGrid = "d-inline-grid";
            public const string Flex = "d-flex";
            public const string InlineFlex = "d-inline-flex";
            public const string Table = "d-table";
            public const string TableRow = "d-table-row";
            public const string TableCell = "d-table-cell";
            public const string None = "d-none";

            /// <summary>
            /// Returns the CSS class name corresponding to the specified display kind value.
            /// </summary>
            /// <remarks>Use this method to map a predefined display kind enumeration to its corresponding
            /// Bootstrap display utility class, typically for use in layout and visibility styling scenarios.</remarks>
            /// <param name="kind">The display kind value for which to retrieve the associated CSS class.</param>
            /// <returns>A string containing the CSS class name that represents the specified display kind.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if the specified display kind value is not defined.</exception>
            public static string Kind(DisplayKind kind) => kind switch
            {
                DisplayKind.Block => Block,
                DisplayKind.Inline => Inline,
                DisplayKind.InlineBlock => InlineBlock,
                DisplayKind.Grid => Grid,
                DisplayKind.InlineGrid => InlineGrid,
                DisplayKind.Flex => Flex,
                DisplayKind.InlineFlex => InlineFlex,
                DisplayKind.Table => Table,
                DisplayKind.TableRow => TableRow,
                DisplayKind.TableCell => TableCell,
                DisplayKind.None => None,
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };

            /// <summary>
            /// Returns the responsive Bootstrap display CSS class name corresponding to the specified breakpoint and display kind values.
            /// </summary>
            /// <remarks>Use this method to generate a Bootstrap display utility class with an optional responsive
            /// breakpoint prefix, typically for use when applying display behavior conditionally by viewport size.</remarks>
            /// <param name="bp">The breakpoint value that determines the responsive prefix to apply.</param>
            /// <param name="kind">The display kind value for which to retrieve the associated CSS class.</param>
            /// <returns>A string containing the CSS class name that represents the specified responsive display utility.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if the specified display kind value is not defined.</exception>
            public static string At(Breakpoint bp, DisplayKind kind)
            {
                var k = kind switch
                {
                    DisplayKind.Inline => "inline",
                    DisplayKind.InlineBlock => "inline-block",
                    DisplayKind.Block => "block",
                    DisplayKind.Grid => "grid",
                    DisplayKind.InlineGrid => "inline-grid",
                    DisplayKind.Flex => "flex",
                    DisplayKind.InlineFlex => "inline-flex",
                    DisplayKind.Table => "table",
                    DisplayKind.TableRow => "table-row",
                    DisplayKind.TableCell => "table-cell",
                    DisplayKind.None => "none",
                    _ => throw new ArgumentOutOfRangeException(nameof(kind))
                };

                return bp == Breakpoint.None
                    ? $"d-{k}"
                    : $"d-{Bp(bp)}-{k}";
            }

            public const string Display1 = "display-1";
            public const string Display2 = "display-2";
            public const string Display3 = "display-3";
            public const string Display4 = "display-4";
            public const string Display5 = "display-5";
            public const string Display6 = "display-6";

            /// <summary>
            /// Returns the CSS class name corresponding to the specified display size value.
            /// </summary>
            /// <remarks>Use this method to map a predefined display size enumeration to its corresponding
            /// Bootstrap display heading class, typically for use in prominently styled heading elements.</remarks>
            /// <param name="size">The display size value for which to retrieve the associated CSS class.</param>
            /// <returns>A string containing the CSS class name that represents the specified display size.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if the specified display size value is not defined.</exception>
            public static string DisplaySize(DisplaySizeKind size) => size switch
            {
                DisplaySizeKind.Display1 => Display1,
                DisplaySizeKind.Display2 => Display2,
                DisplaySizeKind.Display3 => Display3,
                DisplaySizeKind.Display4 => Display4,
                DisplaySizeKind.Display5 => Display5,
                DisplaySizeKind.Display6 => Display6,
                _ => throw new ArgumentOutOfRangeException(nameof(size))
            };
        }
    }
}