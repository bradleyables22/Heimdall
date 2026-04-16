namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides CSS class names and utility methods for working with dropdown components.
        /// </summary>
        /// <remarks>This static class contains constants for commonly used dropdown-related CSS classes,
        /// as well as helper methods for determining dropdown menu alignment. It is intended to centralize dropdown
        /// class names to promote consistency and reduce errors when building UI components.</remarks>
        public static class Dropdown
        {
            public const string Base = "dropdown";
            public const string Toggle = "dropdown-toggle";
            public const string Menu = "dropdown-menu";
            public const string Item = "dropdown-item";
            public const string Divider = "dropdown-divider";
            public const string Header = "dropdown-header";

            public const string MenuEnd = "dropdown-menu-end";
            public const string MenuStart = "dropdown-menu-start";

            /// <summary>
            /// Returns the menu alignment string corresponding to the specified placement value.
            /// </summary>
            /// <param name="p">The placement value that determines the menu alignment. Must be a defined value of the Placement
            /// enumeration.</param>
            /// <returns>A string representing the menu alignment for the specified placement.</returns>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if the specified placement value is not a valid member of the Placement enumeration.</exception>
            public static string MenuAlign(Placement p) => p switch
            {
                Placement.Start => MenuStart,
                Placement.End => MenuEnd,
                _ => throw new ArgumentOutOfRangeException(nameof(p))
            };
        }
    }
}
