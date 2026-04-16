namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Combines the specified CSS class tokens into a single space-delimited string with duplicate removal.
        /// </summary>
        /// <remarks>Use this method to safely compose CSS class strings while automatically removing duplicate
        /// tokens. This is useful when combining multiple helpers or conditional class fragments in UI components.</remarks>
        /// <param name="parts">An array of CSS class strings or fragments to combine.</param>
        /// <returns>A space-delimited string of unique CSS class tokens.</returns>
        public static string Css(params string?[] parts)
            => CssInternal(dedupe: true, parts);

        /// <summary>
        /// Combines the specified CSS class tokens into a single space-delimited string while preserving order and duplicates.
        /// </summary>
        /// <remarks>Use this method when class order is important or when duplicate tokens should be preserved,
        /// such as when working with override or specificity-sensitive CSS scenarios.</remarks>
        /// <param name="parts">An array of CSS class strings or fragments to combine.</param>
        /// <returns>A space-delimited string of CSS class tokens in the order provided.</returns>
        public static string CssOrdered(params string?[] parts)
            => CssInternal(dedupe: false, parts);

        private static string CssInternal(bool dedupe, params string?[] parts)
        {
            if (parts is null || parts.Length == 0)
                return string.Empty;

            var ordered = new List<string>(64);

            if (!dedupe)
            {
                foreach (var p in parts)
                {
                    if (string.IsNullOrWhiteSpace(p)) continue;

                    ordered.AddRange(
                        p.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                }

                return string.Join(' ', ordered);
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var p in parts)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;

                foreach (var t in p.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (seen.Add(t))
                        ordered.Add(t);
                }
            }

            return string.Join(' ', ordered);
        }

        /// <summary>
        /// Returns the provided CSS class string without modification, trimming any surrounding whitespace.
        /// </summary>
        /// <remarks>Use this method when you already have a fully composed CSS class string and want to bypass
        /// any processing or normalization logic.</remarks>
        /// <param name="cssTokens">A preformatted CSS class string.</param>
        /// <returns>The trimmed CSS class string, or an empty string if null.</returns>
        public static string Raw(string cssTokens) => cssTokens?.Trim() ?? string.Empty;

        /// <summary>
        /// Represents responsive breakpoint sizes used in Bootstrap utility classes.
        /// </summary>
        /// <remarks>These values map to Bootstrap's responsive prefixes (e.g., <c>sm</c>, <c>md</c>, <c>lg</c>)
        /// and are used to apply styles conditionally based on viewport size.</remarks>
        public enum Breakpoint { None, Sm, Md, Lg, Xl, Xxl }

        /// <summary>
        /// Represents directional sides used in spacing, positioning, and border utilities.
        /// </summary>
        /// <remarks>These values correspond to Bootstrap directional modifiers such as
        /// top, bottom, start, end, and axis-based (x/y) utilities.</remarks>
        public enum Side { None, Top, Bottom, Start, End, X, Y }

        /// <summary>
        /// Represents display utility types for controlling element layout behavior.
        /// </summary>
        /// <remarks>Maps to Bootstrap display classes such as <c>d-flex</c>, <c>d-grid</c>, and <c>d-inline</c>.</remarks>
        public enum DisplayKind { None, Inline, InlineBlock, Block, Grid, InlineGrid, Flex, InlineFlex, Table, TableRow, TableCell }

        /// <summary>
        /// Represents positioning modes for layout and element placement.
        /// </summary>
        /// <remarks>Maps to Bootstrap position utilities such as <c>position-relative</c> and <c>position-fixed</c>.</remarks>
        public enum PositionKind { Static, Relative, Absolute, Fixed, Sticky }

        /// <summary>
        /// Represents text alignment options.
        /// </summary>
        /// <remarks>Maps to Bootstrap text alignment utilities such as <c>text-start</c>, <c>text-center</c>, and <c>text-end</c>.</remarks>
        public enum TextAlignKind { Start, Center, End }

        /// <summary>
        /// Represents float positioning utilities.
        /// </summary>
        /// <remarks>Maps to Bootstrap float classes such as <c>float-start</c> and <c>float-end</c>.</remarks>
        public enum FloatKind { Start, End, None }

        /// <summary>
        /// Represents overflow behavior for elements.
        /// </summary>
        /// <remarks>Maps to Bootstrap overflow utilities such as <c>overflow-auto</c> and <c>overflow-hidden</c>.</remarks>
        public enum OverflowKind { Auto, Hidden, Visible, Scroll }

        /// <summary>
        /// Represents alignment options for flexbox container items.
        /// </summary>
        /// <remarks>Maps to Bootstrap flex alignment utilities such as <c>align-items-center</c>.</remarks>
        public enum AlignItemsKind { Start, End, Center, Baseline, Stretch }

        /// <summary>
        /// Represents alignment options for individual flexbox items.
        /// </summary>
        /// <remarks>Maps to Bootstrap utilities such as <c>align-self-center</c>.</remarks>
        public enum AlignSelfKind { Auto, Start, End, Center, Baseline, Stretch }

        /// <summary>
        /// Represents justification options for flexbox content.
        /// </summary>
        /// <remarks>Maps to Bootstrap utilities such as <c>justify-content-between</c> and <c>justify-content-center</c>.</remarks>
        public enum JustifyContentKind { Start, End, Center, Between, Around, Evenly }

        /// <summary>
        /// Represents flexbox direction options.
        /// </summary>
        /// <remarks>Maps to Bootstrap utilities such as <c>flex-row</c> and <c>flex-column</c>.</remarks>
        public enum FlexDirectionKind { Row, RowReverse, Column, ColumnReverse }

        /// <summary>
        /// Represents flexbox wrapping behavior.
        /// </summary>
        /// <remarks>Maps to Bootstrap utilities such as <c>flex-wrap</c> and <c>flex-nowrap</c>.</remarks>
        public enum FlexWrapKind { Wrap, Nowrap, WrapReverse }

        /// <summary>
        /// Represents text transformation options.
        /// </summary>
        /// <remarks>Maps to Bootstrap utilities such as <c>text-uppercase</c> and <c>text-capitalize</c>.</remarks>
        public enum TextTransformKind { Lowercase, Uppercase, Capitalize }

        /// <summary>
        /// Represents font weight options.
        /// </summary>
        /// <remarks>Maps to Bootstrap font weight utilities such as <c>fw-bold</c> and <c>fw-light</c>.</remarks>
        public enum FontWeightKind { Light, Lighter, Normal, Medium, Semibold, Bold, Bolder }

        /// <summary>
        /// Represents line height options.
        /// </summary>
        /// <remarks>Maps to Bootstrap line height utilities such as <c>lh-sm</c> and <c>lh-lg</c>.</remarks>
        public enum LineHeightKind { One, Sm, Base, Lg }

        /// <summary>
        /// Represents border radius (rounded) styles.
        /// </summary>
        /// <remarks>Maps to Bootstrap rounding utilities such as <c>rounded</c>, <c>rounded-pill</c>, and <c>rounded-circle</c>.</remarks>
        public enum RoundedKind { None, Sm, Default, Lg, Xl, Xxl, Pill, Circle }

        /// <summary>
        /// Represents shadow styles for elements.
        /// </summary>
        /// <remarks>Maps to Bootstrap shadow utilities such as <c>shadow-sm</c> and <c>shadow-lg</c>.</remarks>
        public enum ShadowKind { None, Sm, Default, Lg }

        /// <summary>
        /// Represents placement directions for positioned elements.
        /// </summary>
        /// <remarks>Commonly used for tooltips, popovers, and dropdown positioning.</remarks>
        public enum Placement { Top, Bottom, Start, End }

        /// <summary>
        /// Represents aspect ratio utilities.
        /// </summary>
        /// <remarks>Maps to Bootstrap ratio classes such as <c>ratio-16x9</c> and <c>ratio-1x1</c>.</remarks>
        public enum RatioKind { R1x1, R4x3, R16x9, R21x9 }

        /// <summary>
        /// Represents font size utilities.
        /// </summary>
        /// <remarks>Maps to Bootstrap font size classes such as <c>fs-1</c> through <c>fs-6</c>.</remarks>
        public enum FontSizeKind { Fs1, Fs2, Fs3, Fs4, Fs5, Fs6 }

        /// <summary>
        /// Represents display heading sizes.
        /// </summary>
        /// <remarks>Maps to Bootstrap display heading classes such as <c>display-1</c> through <c>display-6</c>.</remarks>
        public enum DisplaySizeKind { Display1, Display2, Display3, Display4, Display5, Display6 }

        /// <summary>
        /// Represents Bootstrap theme color values.
        /// </summary>
        /// <remarks>These values are used to map to Bootstrap color-based utility classes such as backgrounds,
        /// text colors, and contextual styling helpers.</remarks>
        public enum Color
        {
            Primary,
            Secondary,
            Success,
            Danger,
            Warning,
            Info,
            Light,
            Dark,

            // Theme/body helpers
            Body,
            BodySecondary,
            BodyTertiary,

            // Only valid for a few helpers (e.g. bg-transparent)
            Transparent
        }

        private static string Bp(Breakpoint bp) => bp switch
        {
            Breakpoint.None => "",
            Breakpoint.Sm => "sm",
            Breakpoint.Md => "md",
            Breakpoint.Lg => "lg",
            Breakpoint.Xl => "xl",
            Breakpoint.Xxl => "xxl",
            _ => throw new ArgumentOutOfRangeException(nameof(bp))
        };
    }
}