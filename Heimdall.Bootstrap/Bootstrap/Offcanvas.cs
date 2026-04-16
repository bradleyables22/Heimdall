namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides Bootstrap offcanvas-related CSS class names for building offcanvas components.
        /// </summary>
        /// <remarks>This class centralizes commonly used Bootstrap offcanvas structure and placement class names,
        /// including container, header, body, and positional variants. It is intended to simplify the process of
        /// applying consistent offcanvas styling in UI components.</remarks>
        public class Offcanvas
        {
            public const string Base = "offcanvas";
            public const string Header = "offcanvas-header";
            public const string Body = "offcanvas-body";
            public const string Title = "offcanvas-title";

            public const string Start = "offcanvas-start";
            public const string End = "offcanvas-end";
            public const string Top = "offcanvas-top";
            public const string Bottom = "offcanvas-bottom";
        }
    }
}