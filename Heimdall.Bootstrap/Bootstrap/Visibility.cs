namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides constants for working with Bootstrap visibility and display utility CSS classes.
        /// </summary>
        /// <remarks>
        /// Use the members of this class to control element visibility and accessibility behavior.
        /// These utilities include standard visibility toggles, visually hidden helpers for screen readers,
        /// and transitional classes such as fade.
        /// 
        /// These are commonly used for accessibility, conditional rendering, and UI transitions.
        /// 
        /// This class is static and cannot be instantiated.
        /// </remarks>
        public static class Visibility
        {
            public const string Visible = "visible";
            public const string Invisible = "invisible";
            public const string VisuallyHidden = "visually-hidden";
            public const string VisuallyHiddenFocusable = "visually-hidden-focusable";
            public const string Fade = "fade";
        }
    }
}