namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides Bootstrap media-related CSS class names and helper methods for styling images and media elements.
        /// </summary>
        /// <remarks>This static class centralizes commonly used Bootstrap media utility class names, including image
        /// responsiveness, thumbnails, aspect ratios, and object-fit behaviors. It also provides helper methods for
        /// generating ratio-based layout classes, enabling consistent media presentation in UI components.</remarks>
        public static class Media
        {
            public const string ImgFluid = "img-fluid";
            public const string ImgThumbnail = "img-thumbnail";

            /// <summary>
            /// Returns the CSS class name corresponding to the specified aspect ratio.
            /// </summary>
            /// <remarks>Use this method to map a predefined ratio enumeration to its corresponding Bootstrap ratio
            /// utility class, typically for maintaining consistent aspect ratios in embedded media or containers.</remarks>
            /// <param name="ratio">The ratio value for which to retrieve the associated CSS class.</param>
            /// <returns>A string containing the CSS class name that represents the specified aspect ratio.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if the specified ratio value is not defined.</exception>
            public static string Ratio(RatioKind ratio) => ratio switch
            {
                RatioKind.R1x1 => "ratio ratio-1x1",
                RatioKind.R4x3 => "ratio ratio-4x3",
                RatioKind.R16x9 => "ratio ratio-16x9",
                RatioKind.R21x9 => "ratio ratio-21x9",
                _ => throw new ArgumentOutOfRangeException(nameof(ratio))
            };

            public const string ObjectFitContain = "object-fit-contain";
            public const string ObjectFitCover = "object-fit-cover";
            public const string ObjectFitFill = "object-fit-fill";
            public const string ObjectFitScale = "object-fit-scale";
            public const string ObjectFitNone = "object-fit-none";
        }
    }
}