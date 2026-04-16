namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides Bootstrap background-related CSS class names and helper methods for generating background and text
        /// background classes based on color values.
        /// </summary>
        /// <remarks>This static class centralizes commonly used Bootstrap background and opacity class
        /// names, as well as utility methods for mapping color values to their corresponding CSS class names. It is
        /// intended to simplify the process of applying consistent background styles in UI components.</remarks>
        public static class Bg
        {
            public const string Body = "bg-body";
            public const string BodySecondary = "bg-body-secondary";
            public const string BodyTertiary = "bg-body-tertiary";
            public const string Transparent = "bg-transparent";
            public const string White = "bg-white";
            public const string Black = "bg-black";

            /// <summary>
            /// Returns the CSS class name corresponding to the specified color value.
            /// </summary>
            /// <remarks>Use this method to map a predefined color enumeration to its corresponding
            /// CSS background class, typically for use in UI styling scenarios.</remarks>
            /// <param name="c">The color value for which to retrieve the associated CSS background class.</param>
            /// <returns>A string containing the CSS class name that represents the specified background color.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if the specified color value is not defined.</exception>
            public static string BgColor(Color c) => c switch
            {
                Color.Primary => "bg-primary",
                Color.Secondary => "bg-secondary",
                Color.Success => "bg-success",
                Color.Danger => "bg-danger",
                Color.Warning => "bg-warning",
                Color.Info => "bg-info",
                Color.Light => "bg-light",
                Color.Dark => "bg-dark",
                Color.Body => Body,
                Color.BodySecondary => BodySecondary,
                Color.BodyTertiary => BodyTertiary,
                Color.Transparent => Transparent,
                _ => throw new ArgumentOutOfRangeException(nameof(c))
            };

            /// <summary>
            /// Returns the subtle Bootstrap background CSS class name corresponding to the specified color value.
            /// </summary>
            /// <remarks>Use this method to map a predefined color enumeration to its corresponding
            /// subtle background utility class, typically for use in UI scenarios requiring softer background emphasis.</remarks>
            /// <param name="c">The color value for which to retrieve the associated subtle background CSS class.</param>
            /// <returns>A string containing the CSS class name that represents the specified subtle background color.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if the specified color value is not supported for subtle backgrounds.</exception>
            public static string Subtle(Color c) => c switch
            {
                Color.Primary => "bg-primary-subtle",
                Color.Secondary => "bg-secondary-subtle",
                Color.Success => "bg-success-subtle",
                Color.Danger => "bg-danger-subtle",
                Color.Warning => "bg-warning-subtle",
                Color.Info => "bg-info-subtle",
                Color.Light => "bg-light-subtle",
                Color.Dark => "bg-dark-subtle",
                _ => throw new ArgumentOutOfRangeException(nameof(c), $"Color '{c}' is not supported for subtle background.")
            };

            public const string Opacity10 = "bg-opacity-10";
            public const string Opacity25 = "bg-opacity-25";
            public const string Opacity50 = "bg-opacity-50";
            public const string Opacity75 = "bg-opacity-75";
            public const string Opacity100 = "bg-opacity-100";

            /// <summary>
            /// Returns the text background CSS class name corresponding to the specified color value.
            /// </summary>
            /// <remarks>Use this method to map a predefined color enumeration to its corresponding
            /// Bootstrap text background utility class, typically for use in badges, alerts, and other emphasized UI elements.</remarks>
            /// <param name="c">The color value for which to retrieve the associated text background CSS class.</param>
            /// <returns>A string containing the CSS class name that represents the specified text background color.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if the specified color value is not supported for text background styling.</exception>
            public static string TextBg(Color c) => c switch
            {
                Color.Primary => "text-bg-primary",
                Color.Secondary => "text-bg-secondary",
                Color.Success => "text-bg-success",
                Color.Danger => "text-bg-danger",
                Color.Warning => "text-bg-warning",
                Color.Info => "text-bg-info",
                Color.Light => "text-bg-light",
                Color.Dark => "text-bg-dark",
                _ => throw new ArgumentOutOfRangeException(nameof(c), $"Color '{c}' is not supported for text background.")
            };
        }
    }
}