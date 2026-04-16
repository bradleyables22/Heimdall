namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides Bootstrap interaction-related CSS class names for controlling user interaction behaviors.
        /// </summary>
        /// <remarks>This static class centralizes commonly used Bootstrap interaction utility class names, including
        /// user selection control, pointer event handling, and collapse behavior. It is intended to simplify the process
        /// of applying consistent interaction-related styling in UI components.</remarks>
        public static class Interaction
        {
            public const string UserSelectAll = "user-select-all";
            public const string UserSelectAuto = "user-select-auto";
            public const string UserSelectNone = "user-select-none";

            public const string PeNone = "pe-none";
            public const string PeAuto = "pe-auto";

            public const string Collapse = "collapse";
        }
    }
}