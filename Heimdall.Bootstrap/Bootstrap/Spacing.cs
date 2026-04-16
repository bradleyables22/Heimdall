namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides helper methods for generating Bootstrap spacing utility CSS classes.
        /// </summary>
        /// <remarks>
        /// Use the members of this class to apply margin, padding, and gap utilities in a
        /// strongly-typed manner. These helpers support directional spacing, responsive
        /// breakpoints, and Bootstrap’s standard spacing scale (<c>0..5</c>).
        /// 
        /// This enables consistent and dynamic composition of layout spacing without
        /// manually constructing class strings.
        /// 
        /// This class is static and cannot be instantiated.
        /// </remarks>
        public static class Spacing
        {
            // margin

            /// <summary>
            /// Returns the CSS class string for applying margin.
            /// </summary>
            /// <param name="n">The spacing size (0–5) based on Bootstrap's spacing scale.</param>
            /// <param name="side">The side or axis to which the margin is applied.</param>
            /// <param name="bp">The optional responsive breakpoint.</param>
            /// <returns>A string containing the Bootstrap margin utility class.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if the spacing value is outside the supported range.</exception>
            public static string M(int n, Side side = Side.None, Breakpoint bp = Breakpoint.None) => Space("m", n, side, bp);

            /// <summary>
            /// Returns the CSS class string for applying horizontal margin.
            /// </summary>
            public static string Mx(int n, Breakpoint bp = Breakpoint.None) => Space("m", n, Side.X, bp);

            /// <summary>
            /// Returns the CSS class string for applying vertical margin.
            /// </summary>
            public static string My(int n, Breakpoint bp = Breakpoint.None) => Space("m", n, Side.Y, bp);

            /// <summary>
            /// Returns the CSS class string for applying top margin.
            /// </summary>
            public static string Mt(int n, Breakpoint bp = Breakpoint.None) => Space("m", n, Side.Top, bp);

            /// <summary>
            /// Returns the CSS class string for applying bottom margin.
            /// </summary>
            public static string Mb(int n, Breakpoint bp = Breakpoint.None) => Space("m", n, Side.Bottom, bp);

            /// <summary>
            /// Returns the CSS class string for applying start (left in LTR) margin.
            /// </summary>
            public static string Ms(int n, Breakpoint bp = Breakpoint.None) => Space("m", n, Side.Start, bp);

            /// <summary>
            /// Returns the CSS class string for applying end (right in LTR) margin.
            /// </summary>
            public static string Me(int n, Breakpoint bp = Breakpoint.None) => Space("m", n, Side.End, bp);

            /// <summary>
            /// Returns the CSS class string for applying automatic margin.
            /// </summary>
            public static string MAuto(Side side = Side.None, Breakpoint bp = Breakpoint.None) => SpaceAuto("m", side, bp);

            /// <summary>
            /// Returns the CSS class string for applying automatic horizontal margin.
            /// </summary>
            public static string MxAuto(Breakpoint bp = Breakpoint.None) => SpaceAuto("m", Side.X, bp);

            /// <summary>
            /// Returns the CSS class string for applying automatic vertical margin.
            /// </summary>
            public static string MyAuto(Breakpoint bp = Breakpoint.None) => SpaceAuto("m", Side.Y, bp);

            /// <summary>
            /// Returns the CSS class string for applying automatic top margin.
            /// </summary>
            public static string MtAuto(Breakpoint bp = Breakpoint.None) => SpaceAuto("m", Side.Top, bp);

            /// <summary>
            /// Returns the CSS class string for applying automatic bottom margin.
            /// </summary>
            public static string MbAuto(Breakpoint bp = Breakpoint.None) => SpaceAuto("m", Side.Bottom, bp);

            /// <summary>
            /// Returns the CSS class string for applying automatic start margin.
            /// </summary>
            public static string MsAuto(Breakpoint bp = Breakpoint.None) => SpaceAuto("m", Side.Start, bp);

            /// <summary>
            /// Returns the CSS class string for applying automatic end margin.
            /// </summary>
            public static string MeAuto(Breakpoint bp = Breakpoint.None) => SpaceAuto("m", Side.End, bp);

            // padding

            /// <summary>
            /// Returns the CSS class string for applying padding.
            /// </summary>
            public static string P(int n, Side side = Side.None, Breakpoint bp = Breakpoint.None) => Space("p", n, side, bp);

            /// <summary>
            /// Returns the CSS class string for applying horizontal padding.
            /// </summary>
            public static string Px(int n, Breakpoint bp = Breakpoint.None) => Space("p", n, Side.X, bp);

            /// <summary>
            /// Returns the CSS class string for applying vertical padding.
            /// </summary>
            public static string Py(int n, Breakpoint bp = Breakpoint.None) => Space("p", n, Side.Y, bp);

            /// <summary>
            /// Returns the CSS class string for applying top padding.
            /// </summary>
            public static string Pt(int n, Breakpoint bp = Breakpoint.None) => Space("p", n, Side.Top, bp);

            /// <summary>
            /// Returns the CSS class string for applying bottom padding.
            /// </summary>
            public static string Pb(int n, Breakpoint bp = Breakpoint.None) => Space("p", n, Side.Bottom, bp);

            /// <summary>
            /// Returns the CSS class string for applying start padding.
            /// </summary>
            public static string Ps(int n, Breakpoint bp = Breakpoint.None) => Space("p", n, Side.Start, bp);

            /// <summary>
            /// Returns the CSS class string for applying end padding.
            /// </summary>
            public static string Pe(int n, Breakpoint bp = Breakpoint.None) => Space("p", n, Side.End, bp);

            // gap

            /// <summary>
            /// Returns the CSS class string for applying gap spacing between elements.
            /// </summary>
            public static string Gap(int n, Breakpoint bp = Breakpoint.None) => GapInternal("gap", n, bp);

            /// <summary>
            /// Returns the CSS class string for applying horizontal gap spacing.
            /// </summary>
            public static string GapX(int n, Breakpoint bp = Breakpoint.None) => GapInternal("column-gap", n, bp);

            /// <summary>
            /// Returns the CSS class string for applying vertical gap spacing.
            /// </summary>
            public static string GapY(int n, Breakpoint bp = Breakpoint.None) => GapInternal("row-gap", n, bp);

            private static string GapInternal(string prefix, int n, Breakpoint bp)
            {
                if (n < 0 || n > 5)
                    throw new ArgumentOutOfRangeException(nameof(n), "Bootstrap scale is 0..5.");

                return bp == Breakpoint.None
                    ? $"{prefix}-{n}"
                    : $"{prefix}-{Bp(bp)}-{n}";
            }

            private static string Space(string prefix, int n, Side side, Breakpoint bp)
            {
                if (n < 0 || n > 5)
                    throw new ArgumentOutOfRangeException(nameof(n), "Bootstrap spacing scale is 0..5.");

                var s = SideToken(side);

                if (bp == Breakpoint.None)
                    return side == Side.None ? $"{prefix}-{n}" : $"{prefix}{s}-{n}";

                return side == Side.None
                    ? $"{prefix}-{Bp(bp)}-{n}"
                    : $"{prefix}{s}-{Bp(bp)}-{n}";
            }

            private static string SpaceAuto(string prefix, Side side, Breakpoint bp)
            {
                var s = SideToken(side);

                if (bp == Breakpoint.None)
                    return side == Side.None ? $"{prefix}-auto" : $"{prefix}{s}-auto";

                return side == Side.None
                    ? $"{prefix}-{Bp(bp)}-auto"
                    : $"{prefix}{s}-{Bp(bp)}-auto";
            }

            private static string SideToken(Side side) => side switch
            {
                Side.None => "",
                Side.Top => "t",
                Side.Bottom => "b",
                Side.Start => "s",
                Side.End => "e",
                Side.X => "x",
                Side.Y => "y",
                _ => throw new ArgumentOutOfRangeException(nameof(side))
            };
        }
    }
}