namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides constants and helper methods for working with Bootstrap text and typography utility CSS classes.
        /// </summary>
        /// <remarks>
        /// Use the members of this class to control text presentation, including alignment, sizing,
        /// transformation, weight, wrapping, color, and decoration. These utilities enable consistent
        /// and dynamic composition of typography-related styles across UI components.
        /// 
        /// Helper methods allow strongly-typed selection of common text behaviors such as alignment,
        /// font size, weight, color variants, and responsive adjustments.
        /// 
        /// This class is static and cannot be instantiated.
        /// </remarks>
        public static class Text
        {
            // Alignment
            public const string Start = "text-start";
            public const string Center = "text-center";
            public const string End = "text-end";

            public const string Fs1 = "fs-1";
            public const string Fs2 = "fs-2";
            public const string Fs3 = "fs-3";
            public const string Fs4 = "fs-4";
            public const string Fs5 = "fs-5";
            public const string Fs6 = "fs-6";

            /// <summary>
            /// Returns the CSS class string corresponding to the specified font size.
            /// </summary>
            /// <param name="size">The font size to apply. Must be a defined value of the FontSizeKind enumeration.</param>
            /// <returns>A string containing the Bootstrap font size utility class.</returns>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Thrown if the specified font size is not supported.
            /// </exception>
            public static string Size(FontSizeKind size) => size switch
            {
                FontSizeKind.Fs1 => Fs1,
                FontSizeKind.Fs2 => Fs2,
                FontSizeKind.Fs3 => Fs3,
                FontSizeKind.Fs4 => Fs4,
                FontSizeKind.Fs5 => Fs5,
                FontSizeKind.Fs6 => Fs6,
                _ => throw new ArgumentOutOfRangeException(nameof(size))
            };

            /// <summary>
            /// Returns the CSS class string for applying text alignment, optionally at a specific breakpoint.
            /// </summary>
            /// <param name="align">The text alignment to apply.</param>
            /// <param name="bp">The optional responsive breakpoint.</param>
            /// <returns>A string containing the Bootstrap text alignment utility class.</returns>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Thrown if the specified alignment is not supported.
            /// </exception>
            public static string Align(TextAlignKind align, Breakpoint bp = Breakpoint.None)
            {
                var t = align switch
                {
                    TextAlignKind.Start => "start",
                    TextAlignKind.Center => "center",
                    TextAlignKind.End => "end",
                    _ => throw new ArgumentOutOfRangeException(nameof(align))
                };

                return bp == Breakpoint.None
                    ? $"text-{t}"
                    : $"text-{Bp(bp)}-{t}";
            }

            // Wrapping / breaking
            public const string Wrap = "text-wrap";
            public const string Nowrap = "text-nowrap";
            public const string Break = "text-break";
            public const string Truncate = "text-truncate";

            // Transform
            public const string Lowercase = "text-lowercase";
            public const string Uppercase = "text-uppercase";
            public const string Capitalize = "text-capitalize";

            /// <summary>
            /// Returns the CSS class string corresponding to the specified text transformation.
            /// </summary>
            /// <param name="t">The text transformation to apply.</param>
            /// <returns>A string containing the Bootstrap text transform utility class.</returns>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Thrown if the specified transformation is not supported.
            /// </exception>
            public static string Transform(TextTransformKind t) => t switch
            {
                TextTransformKind.Lowercase => Lowercase,
                TextTransformKind.Uppercase => Uppercase,
                TextTransformKind.Capitalize => Capitalize,
                _ => throw new ArgumentOutOfRangeException(nameof(t))
            };

            // Weight / style
            public const string FwLight = "fw-light";
            public const string FwLighter = "fw-lighter";
            public const string FwNormal = "fw-normal";
            public const string FwMedium = "fw-medium";
            public const string FwSemibold = "fw-semibold";
            public const string FwBold = "fw-bold";
            public const string FwBolder = "fw-bolder";

            public const string FstItalic = "fst-italic";
            public const string FstNormal = "fst-normal";

            /// <summary>
            /// Returns the CSS class string corresponding to the specified font weight.
            /// </summary>
            /// <param name="w">The font weight to apply.</param>
            /// <returns>A string containing the Bootstrap font weight utility class.</returns>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Thrown if the specified font weight is not supported.
            /// </exception>
            public static string Weight(FontWeightKind w) => w switch
            {
                FontWeightKind.Light => FwLight,
                FontWeightKind.Lighter => FwLighter,
                FontWeightKind.Normal => FwNormal,
                FontWeightKind.Medium => FwMedium,
                FontWeightKind.Semibold => FwSemibold,
                FontWeightKind.Bold => FwBold,
                FontWeightKind.Bolder => FwBolder,
                _ => throw new ArgumentOutOfRangeException(nameof(w))
            };

            // Line-height
            public const string Lh1 = "lh-1";
            public const string LhSm = "lh-sm";
            public const string LhBase = "lh-base";
            public const string LhLg = "lh-lg";

            /// <summary>
            /// Returns the CSS class string corresponding to the specified line height.
            /// </summary>
            /// <param name="lh">The line height to apply.</param>
            /// <returns>A string containing the Bootstrap line-height utility class.</returns>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Thrown if the specified line height is not supported.
            /// </exception>
            public static string LineHeight(LineHeightKind lh) => lh switch
            {
                LineHeightKind.One => Lh1,
                LineHeightKind.Sm => LhSm,
                LineHeightKind.Base => LhBase,
                LineHeightKind.Lg => LhLg,
                _ => throw new ArgumentOutOfRangeException(nameof(lh))
            };

            // Misc typography
            public const string Lead = "lead";
            public const string Small = "small";
            public const string Mark = "mark";
            public const string Monospace = "font-monospace";

            // Colors
            public const string Muted = "text-muted";
            public const string Body = "text-body";
            public const string BodySecondary = "text-body-secondary";
            public const string BodyTertiary = "text-body-tertiary";
            public const string BodyEmphasis = "text-body-emphasis";
            public const string Reset = "text-reset";
            public const string White = "text-white";
            public const string Black = "text-black";
            public const string Black50 = "text-black-50";
            public const string White50 = "text-white-50";

            /// <summary>
            /// Returns the CSS class string corresponding to the specified text color.
            /// </summary>
            /// <param name="c">The color to apply.</param>
            /// <returns>A string containing the Bootstrap text color utility class.</returns>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Thrown if the specified color is not supported.
            /// </exception>
            public static string TxtColor(Color c) => c switch
            {
                Color.Primary => "text-primary",
                Color.Secondary => "text-secondary",
                Color.Success => "text-success",
                Color.Danger => "text-danger",
                Color.Warning => "text-warning",
                Color.Info => "text-info",
                Color.Light => "text-light",
                Color.Dark => "text-dark",
                Color.Body => Body,
                Color.BodySecondary => BodySecondary,
                Color.BodyTertiary => BodyTertiary,
                _ => throw new ArgumentOutOfRangeException(nameof(c))
            };

            /// <summary>
            /// Returns the CSS class string corresponding to the specified emphasis color.
            /// </summary>
            /// <param name="c">The color to apply as emphasis.</param>
            /// <returns>A string containing the Bootstrap emphasis text color class.</returns>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Thrown if the specified color is not supported for emphasis.
            /// </exception>
            public static string EmphasisColor(Color c) => c switch
            {
                Color.Primary => "text-primary-emphasis",
                Color.Secondary => "text-secondary-emphasis",
                Color.Success => "text-success-emphasis",
                Color.Danger => "text-danger-emphasis",
                Color.Warning => "text-warning-emphasis",
                Color.Info => "text-info-emphasis",
                Color.Light => "text-light-emphasis",
                Color.Dark => "text-dark-emphasis",
                _ => throw new ArgumentOutOfRangeException(nameof(c), $"Color '{c}' is not supported for text emphasis.")
            };

            // Opacity
            public const string Opacity25 = "text-opacity-25";
            public const string Opacity50 = "text-opacity-50";
            public const string Opacity75 = "text-opacity-75";
            public const string Opacity100 = "text-opacity-100";

            public enum TextOpacity
            {
                Opacity25,
                Opacity50,
                Opacity75,
                Opacity100
            }

            /// <summary>
            /// Returns the CSS class string corresponding to the specified text opacity.
            /// </summary>
            /// <param name="o">The opacity level to apply.</param>
            /// <returns>A string containing the Bootstrap text opacity utility class.</returns>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Thrown if the specified opacity is not supported.
            /// </exception>
            public static string Opacity(TextOpacity o) => o switch
            {
                TextOpacity.Opacity25 => Opacity25,
                TextOpacity.Opacity50 => Opacity50,
                TextOpacity.Opacity75 => Opacity75,
                TextOpacity.Opacity100 => Opacity100,
                _ => throw new ArgumentOutOfRangeException(nameof(o))
            };

            // Decorations
            public const string DecorationNone = "text-decoration-none";
            public const string DecorationUnderline = "text-decoration-underline";
            public const string DecorationLineThrough = "text-decoration-line-through";

            // Links
            public const string LinkOpacity10 = "link-opacity-10";
            public const string LinkOpacity25 = "link-opacity-25";
            public const string LinkOpacity50 = "link-opacity-50";
            public const string LinkOpacity75 = "link-opacity-75";
            public const string LinkOpacity100 = "link-opacity-100";

            public const string LinkUnderline = "link-underline";
            public const string LinkUnderlineOpacity0 = "link-underline-opacity-0";
            public const string LinkUnderlineOpacity10 = "link-underline-opacity-10";
            public const string LinkUnderlineOpacity25 = "link-underline-opacity-25";
            public const string LinkUnderlineOpacity50 = "link-underline-opacity-50";
            public const string LinkUnderlineOpacity75 = "link-underline-opacity-75";
            public const string LinkUnderlineOpacity100 = "link-underline-opacity-100";
            public const string StretchedLink = "stretched-link";

            /// <summary>
            /// Returns the CSS class string corresponding to the specified link color.
            /// </summary>
            /// <param name="c">The color to apply to the link.</param>
            /// <returns>A string containing the Bootstrap link color utility class.</returns>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Thrown if the specified color is not supported for link styling.
            /// </exception>
            public static string LinkColor(Color c) => c switch
            {
                Color.Primary => "link-primary",
                Color.Secondary => "link-secondary",
                Color.Success => "link-success",
                Color.Danger => "link-danger",
                Color.Warning => "link-warning",
                Color.Info => "link-info",
                Color.Light => "link-light",
                Color.Dark => "link-dark",
                _ => throw new ArgumentOutOfRangeException(nameof(c), $"Color '{c}' is not supported for link color.")
            };
        }
    }
}