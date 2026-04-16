namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides Bootstrap list group-related CSS class names and helper methods for building list group components.
        /// </summary>
        /// <remarks>This static class centralizes commonly used Bootstrap list group class names, including layout,
        /// styling, and interaction variants. It also provides helper methods for generating responsive list group
        /// classes, enabling consistent and flexible list-based UI components.</remarks>
        public static class ListGroup
        {
            public const string Base = "list-group";
            public const string Item = "list-group-item";
            public const string Flush = "list-group-flush";
            public const string Numbered = "list-group-numbered";
            public const string Horizontal = "list-group-horizontal";
            public const string ItemAction = "list-group-item-action";

            /// <summary>
            /// Returns the CSS class name for a horizontal list group at the specified breakpoint.
            /// </summary>
            /// <remarks>Use this method to generate responsive horizontal list group classes such as
            /// <c>list-group-horizontal</c> or <c>list-group-horizontal-md</c>, typically for arranging list items in a row.</remarks>
            /// <param name="bp">The breakpoint at which the horizontal layout should apply.</param>
            /// <returns>A string containing the CSS class name representing the horizontal list group.</returns>
            public static string HorizontalAt(Breakpoint bp)
            {
                if (bp == Breakpoint.None)
                    return Horizontal;

                return $"list-group-horizontal-{Bp(bp)}";
            }
        }
    }
}