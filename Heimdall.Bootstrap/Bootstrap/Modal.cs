namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides Bootstrap modal-related CSS class names and helper methods for building modal components.
        /// </summary>
        /// <remarks>This static class centralizes commonly used Bootstrap modal structure, sizing, and behavior
        /// class names. It also provides helper methods for generating responsive modal classes, enabling consistent
        /// and flexible modal implementations in UI components.</remarks>
        public static class Modal
        {
            public const string ModalBase = "modal";
            public const string Dialog = "modal-dialog";
            public const string Content = "modal-content";
            public const string Header = "modal-header";
            public const string Body = "modal-body";
            public const string Footer = "modal-footer";
            public const string Title = "modal-title";
            public const string Backdrop = "modal-backdrop";

            public const string DialogCentered = "modal-dialog-centered";
            public const string DialogScrollable = "modal-dialog-scrollable";

            public const string Sm = "modal-sm";
            public const string Lg = "modal-lg";
            public const string Xl = "modal-xl";
            public const string Fullscreen = "modal-fullscreen";

            /// <summary>
            /// Returns the CSS class name for a fullscreen modal at the specified breakpoint.
            /// </summary>
            /// <remarks>Use this method to generate responsive fullscreen modal classes such as
            /// <c>modal-fullscreen</c> or <c>modal-fullscreen-md-down</c>, typically for controlling modal sizing
            /// behavior across different viewport sizes.</remarks>
            /// <param name="bp">The breakpoint at which the fullscreen behavior should apply.</param>
            /// <returns>A string containing the CSS class name representing the fullscreen modal behavior.</returns>
            public static string FullscreenAt(Breakpoint bp)
            {
                if (bp == Breakpoint.None)
                    return Fullscreen;

                return $"modal-fullscreen-{Bp(bp)}-down";
            }
        }
    }
}