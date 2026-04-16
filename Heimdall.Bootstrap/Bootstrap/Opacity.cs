namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides constants for working with Bootstrap opacity utility CSS classes.
        /// </summary>
        /// <remarks>
        /// Use the members of this class to apply predefined opacity levels to elements.
        /// These utility classes control the transparency of an element, ranging from fully transparent
        /// (<c>opacity-0</c>) to fully opaque (<c>opacity-100</c>).
        /// This class is static and cannot be instantiated.
        /// </remarks>
        public static class Opacity
        {
            public const string Opacity0 = "opacity-0";
            public const string Opacity25 = "opacity-25";
            public const string Opacity50 = "opacity-50";
            public const string Opacity75 = "opacity-75";
            public const string Opacity100 = "opacity-100";
        }
    }
}