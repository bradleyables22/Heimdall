namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides a set of constants and helper methods for generating CSS class names related to flexbox layout and
        /// alignment in web applications.
        /// </summary>
        /// <remarks>The Flex class centralizes commonly used flexbox-related CSS class names and offers
        /// methods to construct responsive class names based on layout options and breakpoints. This is useful for
        /// building UI components that require dynamic or responsive flexbox styling. All members are static and
        /// intended for use in generating class attributes for HTML elements.</remarks>
        public static class Flex
        {
            // Direction / wrap
            public const string Row = "flex-row";
            public const string RowReverse = "flex-row-reverse";
            public const string Column = "flex-column";
            public const string ColumnReverse = "flex-column-reverse";

            public const string Wrap = "flex-wrap";
            public const string Nowrap = "flex-nowrap";
            public const string WrapReverse = "flex-wrap-reverse";

            // Grow / shrink
            public const string Grow0 = "flex-grow-0";
            public const string Grow1 = "flex-grow-1";
            public const string Shrink0 = "flex-shrink-0";
            public const string Shrink1 = "flex-shrink-1";

            //others
            public const string HStack = "hstack";
            public const string VStack = "vstack";
            public const string Vr = "vr";

            // Fill
            public const string Fill = "flex-fill";

            // Align items
            public const string AlignItemsStart = "align-items-start";
            public const string AlignItemsEnd = "align-items-end";
            public const string AlignItemsCenter = "align-items-center";
            public const string AlignItemsBaseline = "align-items-baseline";
            public const string AlignItemsStretch = "align-items-stretch";

            // Align self
            public const string AlignSelfAuto = "align-self-auto";
            public const string AlignSelfStart = "align-self-start";
            public const string AlignSelfEnd = "align-self-end";
            public const string AlignSelfCenter = "align-self-center";
            public const string AlignSelfBaseline = "align-self-baseline";
            public const string AlignSelfStretch = "align-self-stretch";

            // Align content
            public const string AlignContentStart = "align-content-start";
            public const string AlignContentEnd = "align-content-end";
            public const string AlignContentCenter = "align-content-center";
            public const string AlignContentBetween = "align-content-between";
            public const string AlignContentAround = "align-content-around";
            public const string AlignContentStretch = "align-content-stretch";

            // Justify content
            public const string JustifyStart = "justify-content-start";
            public const string JustifyEnd = "justify-content-end";
            public const string JustifyCenter = "justify-content-center";
            public const string JustifyBetween = "justify-content-between";
            public const string JustifyAround = "justify-content-around";
            public const string JustifyEvenly = "justify-content-evenly";

            // Order
            public const string OrderFirst = "order-first";
            public const string OrderLast = "order-last";
            public const string Order0 = "order-0";
            public const string Order1 = "order-1";
            public const string Order2 = "order-2";
            public const string Order3 = "order-3";
            public const string Order4 = "order-4";
            public const string Order5 = "order-5";

            public const string CenterContent = "d-flex justify-content-center align-items-center";
            public const string CenterBlock = "mx-auto";

            /// <summary>
            /// Returns a CSS spacing value representing a gap of the specified size, optionally adjusted for a given
            /// breakpoint.
            /// </summary>
            /// <param name="n">The size of the gap to generate. Must be a non-negative integer.</param>
            /// <param name="bp">An optional breakpoint at which the gap should apply. Defaults to Breakpoint.None if not specified.</param>
            /// <returns>A string containing the CSS value for the gap, formatted according to the specified size and breakpoint.</returns>
            public static string Gap(int n, Breakpoint bp = Breakpoint.None) => Spacing.Gap(n, bp);

            /// <summary>
            /// Returns the CSS class name corresponding to the specified flex direction and optional breakpoint.
            /// </summary>
            /// <remarks>Use this method to map a predefined flex direction enumeration to its corresponding
            /// Bootstrap flex direction utility class, optionally scoped to a responsive breakpoint.</remarks>
            /// <param name="dir">The flex direction to use when generating the class name.</param>
            /// <param name="bp">The responsive breakpoint to apply. If not specified, no breakpoint prefix is added.</param>
            /// <returns>A string containing the CSS class name representing the specified flex direction, optionally prefixed with the breakpoint.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if dir is not a valid value of FlexDirectionKind.</exception>
            public static string Direction(FlexDirectionKind dir, Breakpoint bp = Breakpoint.None)
            {
                var t = dir switch
                {
                    FlexDirectionKind.Row => "row",
                    FlexDirectionKind.RowReverse => "row-reverse",
                    FlexDirectionKind.Column => "column",
                    FlexDirectionKind.ColumnReverse => "column-reverse",
                    _ => throw new ArgumentOutOfRangeException(nameof(dir))
                };

                return bp == Breakpoint.None
                    ? $"flex-{t}"
                    : $"flex-{Bp(bp)}-{t}";
            }

            /// <summary>
            /// Generates a CSS class name for the specified flex wrap mode and optional breakpoint.
            /// </summary>
            /// <remarks>Use this method to map a predefined wrap mode enumeration to its corresponding
            /// Bootstrap flex wrap utility class, optionally scoped to a responsive breakpoint.</remarks>
            /// <param name="wrap">The flex wrap mode to use when generating the class name.</param>
            /// <param name="bp">The optional breakpoint at which the wrap mode should apply.</param>
            /// <returns>A string containing the CSS class name representing the specified flex wrap mode.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if wrap is not a valid value of FlexWrapKind.</exception>
            public static string WrapMode(FlexWrapKind wrap, Breakpoint bp = Breakpoint.None)
            {
                var t = wrap switch
                {
                    FlexWrapKind.Wrap => "wrap",
                    FlexWrapKind.Nowrap => "nowrap",
                    FlexWrapKind.WrapReverse => "wrap-reverse",
                    _ => throw new ArgumentOutOfRangeException(nameof(wrap))
                };

                return bp == Breakpoint.None
                    ? $"flex-{t}"
                    : $"flex-{Bp(bp)}-{t}";
            }

            /// <summary>
            /// Generates a CSS class name for the specified justify-content alignment and optional breakpoint.
            /// </summary>
            /// <remarks>Use this method to map a predefined justification enumeration to its corresponding
            /// Bootstrap justify-content utility class, optionally scoped to a responsive breakpoint.</remarks>
            /// <param name="justify">The justify-content alignment to use when generating the CSS class name.</param>
            /// <param name="bp">The responsive breakpoint to include as a prefix in the CSS class name.</param>
            /// <returns>A string containing the CSS class name representing the specified justify-content alignment.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if justify is not a valid value of JustifyContentKind.</exception>
            public static string Justify(JustifyContentKind justify, Breakpoint bp = Breakpoint.None)
            {
                var t = justify switch
                {
                    JustifyContentKind.Start => "start",
                    JustifyContentKind.End => "end",
                    JustifyContentKind.Center => "center",
                    JustifyContentKind.Between => "between",
                    JustifyContentKind.Around => "around",
                    JustifyContentKind.Evenly => "evenly",
                    _ => throw new ArgumentOutOfRangeException(nameof(justify))
                };

                return bp == Breakpoint.None
                    ? $"justify-content-{t}"
                    : $"justify-content-{Bp(bp)}-{t}";
            }

            /// <summary>
            /// Generates a CSS class name for the specified align-items value and optional breakpoint.
            /// </summary>
            /// <remarks>Use this method to map a predefined alignment enumeration to its corresponding
            /// Bootstrap align-items utility class, optionally scoped to a responsive breakpoint.</remarks>
            /// <param name="align">The alignment option to use for the align-items CSS property.</param>
            /// <param name="bp">An optional responsive breakpoint at which the alignment should apply.</param>
            /// <returns>A string containing the CSS class name representing the specified align-items alignment.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if align is not a valid value of AlignItemsKind.</exception>
            public static string AlignItems(AlignItemsKind align, Breakpoint bp = Breakpoint.None)
            {
                var t = align switch
                {
                    AlignItemsKind.Start => "start",
                    AlignItemsKind.End => "end",
                    AlignItemsKind.Center => "center",
                    AlignItemsKind.Baseline => "baseline",
                    AlignItemsKind.Stretch => "stretch",
                    _ => throw new ArgumentOutOfRangeException(nameof(align))
                };

                return bp == Breakpoint.None
                    ? $"align-items-{t}"
                    : $"align-items-{Bp(bp)}-{t}";
            }

            /// <summary>
            /// Generates a CSS class name for the specified align-self value and optional breakpoint.
            /// </summary>
            /// <remarks>Use this method to map a predefined alignment enumeration to its corresponding
            /// Bootstrap align-self utility class, optionally scoped to a responsive breakpoint.</remarks>
            /// <param name="align">The alignment option to use for the align-self CSS property.</param>
            /// <param name="bp">An optional responsive breakpoint at which the alignment should apply.</param>
            /// <returns>A string containing the CSS class name representing the specified align-self alignment.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if align is not a valid value of AlignSelfKind.</exception>
            public static string AlignSelf(AlignSelfKind align, Breakpoint bp = Breakpoint.None)
            {
                var t = align switch
                {
                    AlignSelfKind.Auto => "auto",
                    AlignSelfKind.Start => "start",
                    AlignSelfKind.End => "end",
                    AlignSelfKind.Center => "center",
                    AlignSelfKind.Baseline => "baseline",
                    AlignSelfKind.Stretch => "stretch",
                    _ => throw new ArgumentOutOfRangeException(nameof(align))
                };

                return bp == Breakpoint.None
                    ? $"align-self-{t}"
                    : $"align-self-{Bp(bp)}-{t}";
            }
        }
    }
}